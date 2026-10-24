# Recorre los eventos de MS3 de punta a punta contra un stack ya levantado:
#   1. Simula a MS2 publicando un plan y una receta en RabbitMQ.
#   2. Crea un contrato con ese plan y lo paga  -> POST /schedule a MS4 (REST, desde el outbox)
#   3. Arma, etiqueta y valida un paquete      -> PackageValidated  en ms5.package-validated
#
# Standalone (docker compose up --build en la raíz del repo):
#   pwsh ./scripts/demo-eventos.ps1
# Plataforma (nur-tricenter-local, entrando por el gateway):
#   pwsh ./scripts/demo-eventos.ps1 -ApiBase http://localhost:15005/api/contracting

param(
    [string]$ApiBase = 'http://localhost:8080/api',
    [string]$RabbitBase = 'http://localhost:15672',
    [string]$RabbitUser = 'guest',
    [string]$RabbitPassword = 'guest',
    [int]$EsperaSegundos = 8
)

$ErrorActionPreference = 'Stop'

$auth = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitUser}:${RabbitPassword}")) }
$paciente = '00000000-0000-0000-0000-000000000001'
$planId = [guid]::NewGuid().ToString()
$recetaId = [guid]::NewGuid().ToString()
$sufijo = (Get-Date -Format 'HHmmss')

function Publicar-EnRabbit([string]$exchange, [hashtable]$mensaje) {
    $body = @{ properties = @{ content_type = 'application/json' }; routing_key = ''; payload = ($mensaje | ConvertTo-Json -Compress); payload_encoding = 'string' } | ConvertTo-Json
    $r = Invoke-RestMethod -Method Post -Uri "$RabbitBase/api/exchanges/%2F/$exchange/publish" -Headers $auth -ContentType 'application/json' -Body $body
    if (-not $r.routed) { throw "El exchange '$exchange' no tiene colas enlazadas: el mensaje se perdió." }
}

function Leer-Cola([string]$cola) {
    $body = @{ count = 10; ackmode = 'ack_requeue_true'; encoding = 'auto' } | ConvertTo-Json
    @(Invoke-RestMethod -Method Post -Uri "$RabbitBase/api/queues/%2F/$cola/get" -Headers $auth -ContentType 'application/json' -Body $body) |
        ForEach-Object { $_.payload | ConvertFrom-Json }
}

function Api([string]$metodo, [string]$ruta, $cuerpo) {
    $params = @{ Method = $metodo; Uri = "$ApiBase$ruta" }
    if ($cuerpo) {
        $params.ContentType = 'application/json'
        $params.Body = ($cuerpo | ConvertTo-Json)
    }
    try {
        Invoke-RestMethod @params
    }
    catch {
        $detalle = $_.ErrorDetails.Message
        if (-not $detalle -and $_.Exception.Response) {
            $detalle = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
        }
        throw "$metodo $ruta fallo: $($_.Exception.Message) $detalle"
    }
}

Write-Host "`n[1] MS2 publica MealPlanPublished y RecipePublished" -ForegroundColor Cyan
Publicar-EnRabbit 'meal-plan-published' @{
    PlanId = $planId; Name = "Plan demo $sufijo"; DurationType = 'Monthly'; TotalDays = 30; MealsPerDay = 3
    EventType = 'MealPlan.Published'; Id = [guid]::NewGuid().ToString(); CreatedAt = (Get-Date).ToUniversalTime().ToString('o'); CorrelationId = "demo-$sufijo"; Source = 'catalog'
}
Publicar-EnRabbit 'recipe-published' @{
    RecipeId = $recetaId; Name = "Receta demo $sufijo"; Instructions = 'Hervir 20 min'
    EventType = 'Recipe.Published'; Id = [guid]::NewGuid().ToString(); CreatedAt = (Get-Date).ToUniversalTime().ToString('o'); CorrelationId = "demo-$sufijo"; Source = 'catalog'
}
Start-Sleep -Seconds 3
Write-Host "    plan $planId publicado"

Write-Host "`n[2] Contrato con el plan de MS2 y pago" -ForegroundColor Cyan
$contrato = Api Post '/v1/contracts' @{ patientId = $paciente; catalogPlanId = $planId }
Write-Host "    contrato $($contrato.id) -> $($contrato.status)"
$factura = "inv-demo-$sufijo"
$pago = Api Post '/v1/billing/charge-plan' @{ contractId = $contrato.id; amount = 450.00; invoiceNumber = $factura }
Write-Host "    pago $($pago.status)"
$detalle = Api Get "/v1/billing/$factura"
Write-Host "    factura con planName='$($detalle.planName)' (viene de la proyeccion de MS2)"

Write-Host "`n[3] Produccion: orden, paquete, armado, etiqueta y validacion" -ForegroundColor Cyan
$fecha = (Get-Date).ToUniversalTime().AddDays((Get-Random -Minimum 30 -Maximum 3000)).ToString('yyyy-MM-dd')
$orden = Api Get "/v1/production/daily-order?productionDate=$fecha"
$paquete = Api Post '/v1/production/packages' @{ productionOrderId = $orden.productionOrderId; patientId = $paciente; catalogPlanId = $planId }
Api Post '/v1/production/packages/assemble' @{ packageId = $paquete.packageId; staffId = 'staff-demo' } | Out-Null
Api Post '/v1/production/packages/label' @{ packageId = $paquete.packageId; patientId = $paciente } | Out-Null
$validacion = Api Post '/v1/production/packages/validate' @{ productionOrderId = $orden.productionOrderId; supervisorId = 'sup-demo'; batchValidated = $true; totalValidatedCount = 1 }
Write-Host "    paquete $($paquete.packageId) validado: $($validacion.validatedCount)/$($validacion.totalPackages)"

Write-Host "`n[4] Esperando $EsperaSegundos s a que el worker drene el outbox ..." -ForegroundColor Cyan
Start-Sleep -Seconds $EsperaSegundos

$filaMs4 = (Api Get '/v1/sim/outgoing/CreateSchedule') | ForEach-Object { $_ } | Where-Object { $_.payload -like "*$($contrato.id)*" } | Select-Object -First 1
$validado = Leer-Cola 'ms5.package-validated' | Where-Object { $_.PackageId -eq $paquete.packageId }

Write-Host "`n[5] Calendario en MS4 (fila CreateSchedule del outbox)" -ForegroundColor Green
switch ($filaMs4.status) {
    'Published'  { Write-Host "    Published: MS4 creo el calendario (POST /schedule respondio 200)" }
    'Unroutable' { Write-Host "    Unroutable: MS4 no esta configurado (modo standalone)" -ForegroundColor Yellow }
    'Rejected'   { Write-Host "    Rejected: MS4 rechazo el calendario (duracion distinta de 15/30 o plan desconocido)" -ForegroundColor Red }
    default      { Write-Host "    Pendiente: MS4 o identity no respondieron todavia; el worker reintenta" -ForegroundColor Red }
}
Write-Host "`n[6] Lo que recibiria MS5 (ms5.package-validated)" -ForegroundColor Green
if ($validado) { $validado | ConvertTo-Json } else { Write-Host '    NO LLEGO' -ForegroundColor Red }

if (-not $validado -or $filaMs4.status -notin @('Published', 'Unroutable')) { exit 1 }
Write-Host "`nOK: MS3 consumio de MS2 y publico hacia MS4 y MS5." -ForegroundColor Green
