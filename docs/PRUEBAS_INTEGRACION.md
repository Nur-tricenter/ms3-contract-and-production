# Cómo probar la comunicación de MS3 y qué esperar de cada evento

Guía práctica para verificar que MS3 (Contratación y Producción) habla con el resto
de la plataforma por RabbitMQ. Complementa a
[INTEGRACION_PLATAFORMA.md](INTEGRACION_PLATAFORMA.md), que explica el diseño.

Todo lo de esta guía se ejecutó el 2026-10-23 contra contenedores reales (Docker
29.9, RabbitMQ 3.13, PostgreSQL 16, Consul 1.20); los resultados están al final.

## Requisitos

| Qué | Para qué | Cómo verificar |
|---|---|---|
| Docker Desktop corriendo | Postgres, RabbitMQ, Consul y los contenedores de MS3 | `docker version` muestra Client y Server |
| .NET SDK 8 (y 10 para el gateway) | Compilar MS3 y el gateway antes de levantar la plataforma | `dotnet --list-sdks` |
| Puertos libres | 5672/15672 (RabbitMQ), 8080 o 15005 (API/gateway), 5433/5434 (Postgres), 8500 (Consul) | `docker ps` sin otro stack levantado |
| `NUGET_TOKEN` (opcional) | Solo para compilar catálogo e identity reales; MS3 no lo necesita | `dotnet restore` del catálogo sin 401 |

Los dos stacks usan los mismos puertos de RabbitMQ: levanta uno a la vez.

## Nivel 1 — Pruebas automáticas (sin Docker)

```bash
dotnet test Nurtricenter.MS3.sln
```

Las pruebas de `Nurtricenter.MS3.IntegrationTests/Messaging/` cubren la mensajería
sin broker: el relay publica en un `BrokerEnMemoria` y la base es SQLite in-memory.

| Clase | Qué demuestra |
|---|---|
| `OutboxRelayTests` | `CreateSchedule` → `ContractActivated` en `contract-activated`; `TransferToLogistics` → `PackageValidated` en `package-validated`; no republica; tipo desconocido o payload inválido → `Unroutable`; broker caído → la fila queda pendiente |
| `CatalogConsumersTests` | Proyección de planes y recetas; redelivery con el mismo `Id` se descarta (inbox); deserializa el JSON exacto que publica el catálogo |
| `CatalogProjectionFlowTests` | Por HTTP: contrato con un plan que solo existe en la proyección → 201; pago → el relay publica `ContractActivated` con ese plan |

## Nivel 2 — MS3 solo, con su propio RabbitMQ

```bash
docker compose up --build -d
pwsh ./scripts/demo-eventos.ps1
```

En Windows PowerShell 5.1 sirve igual: `.\scripts\demo-eventos.ps1`.

| URL | Qué hay |
|---|---|
| `http://localhost:8080/swagger` | API de MS3 |
| `http://localhost:15672` (guest/guest) | RabbitMQ: exchanges, colas y mensajes |
| `localhost:5434` (dev-only / dev-only-password) | Base `nur_tricenter_contracting` |

Para bajarlo: `docker compose down` (agrega `-v` para borrar también la base).

## Nivel 3 — Dentro de la plataforma del equipo

Requiere `nur-tricenter-local` en la rama `feat/ms3-contracting`, al lado de este
repositorio.

Stack completo, con catálogo e identity (necesita `NUGET_TOKEN`):

```bash
cd ../nur-tricenter-local
pwsh ./scripts/start-stack.ps1
```

Sin token: solo infraestructura, gateway y MS3.

```bash
dotnet build Nurtricenter.Api -c Debug
dotnet build Nurtricenter.MS3.WorkerService -c Debug
dotnet build ../nur-tricenter-api-gateway/ApiGateway.App -c Debug
docker network create nur-tricenter-net
cd ../nur-tricenter-local
docker compose -f compose/docker-compose.yml -f compose/docker-compose.infra.yml -p nur-tricenter up -d contracting-api contracting-workerservice gateway
```

Luego, desde la raíz de `ms3-contract-and-production`:

```bash
pwsh ./scripts/demo-eventos.ps1 -ApiBase http://localhost:15005/api/contracting
```

| URL | Qué hay |
|---|---|
| `http://localhost:15005/api/contracting/v1/...` | MS3 a través del gateway (YARP) |
| `http://localhost:8500` | Consul: debe aparecer el servicio `contracting` en verde |
| `http://localhost:15672` | RabbitMQ de la plataforma |

El gateway refresca sus rutas desde Consul cada 30 s: un 404 en el primer medio
minuto es normal.

## Qué hace el script de demo, paso a paso

1. **Simula a MS2** publicando `MealPlanPublished` y `RecipePublished` en sus
   exchanges, con la misma forma JSON que el catálogo real.
2. **Crea un contrato** con ese plan (solo existe gracias a la proyección) y lo
   **paga**. La factura muestra el nombre del plan que llegó de MS2.
3. **Producción:** genera la orden diaria, crea, arma, etiqueta y valida un paquete.
4. Espera a que el worker drene el outbox y **lee las colas `ms4.contract-activated`
   y `ms5.package-validated`** sin consumir los mensajes (`ack_requeue_true`).
5. Imprime exactamente lo que recibirían MS4 y MS5, y termina con `OK`, o con
   código 1 si algo no llegó.

## Qué esperar de cada evento

### Eventos que MS3 consume

| De | Exchange → cola de MS3 | Contrato | Qué hace MS3 | Cómo verificarlo |
|---|---|---|---|---|
| MS2 catálogo | `meal-plan-published` → `ms3.meal-plan-published` | `MealPlanPublished` | Upsert en `CatalogPlans`. Desde ese momento `POST /v1/contracts` acepta ese `PlanId` y la factura muestra su nombre. | `select * from "CatalogPlans";` y fila en `"InboxMessages"` |
| MS2 catálogo | `recipe-published` → `ms3.recipe-published` | `RecipePublished` | Upsert en `CatalogRecipes`; la orden diaria usa el nombre real de la receta | `select * from "CatalogRecipes";` |
| MS2 catálogo | `recipe-updated` → `ms3.recipe-updated` | `RecipeUpdated` | Actualiza nombre e instrucciones de la receta | Igual que el anterior |

Forma esperada de `MealPlanPublished` (la publica el catálogo real al sembrar
datos y al crear un plan):

```json
{
  "PlanId": "guid", "Name": "Plan Keto", "DurationType": "Monthly",
  "TotalDays": 30, "MealsPerDay": 3, "EventType": "MealPlan.Published",
  "Id": "guid", "CreatedAt": "2026-10-23T12:00:00+00:00",
  "CorrelationId": "…", "Source": "catalog"
}
```

Reglas que cumple MS3 al consumir:

- **Idempotencia por `Id`:** si llega dos veces el mismo `Id`, la segunda se ignora
  (log `Skipping … already in the inbox`).
- **Un `Id` nuevo para el mismo `PlanId`** actualiza la proyección.
- Si el handler falla, Joseco reintenta (`MaxRetryAttempts`, 3) y luego manda el
  mensaje a la cola de error de esa cola.

### Eventos que MS3 publica

#### `ContractActivated` → MS4 (calendario de entregas)

- **Cuándo:** al cobrar un contrato (`POST /v1/billing/charge-plan`), que pasa de
  `PendingPayment` a `Active`.
- **Dónde:** exchange `contract-activated` → cola `ms4.contract-activated`.

```json
{
  "EventType": "Contract.Activated",
  "ContractId": "guid", "PatientId": "guid", "CatalogPlanId": "guid",
  "InvoiceNumber": "inv-demo-081522", "TotalAmount": 450.0,
  "Id": "guid", "CreatedAt": "…", "CorrelationId": "guid", "Source": "contracting"
}
```

Qué debería hacer MS4 cuando lo implementen:

- Consumir `ms4.contract-activated` y crear el calendario de entregas del paciente
  para ese plan.
- Deduplicar por `Id`: puede llegar dos veces si el worker de MS3 se reinicia
  entre publicar y marcar la fila.

#### `PackageValidated` → MS5 (logística)

- **Cuándo:** al aprobar el control de calidad (`POST /v1/production/packages/validate`);
  un evento por paquete aprobado.
- **Dónde:** exchange `package-validated` → cola `ms5.package-validated`.

```json
{
  "EventType": "Package.Validated",
  "PackageId": "guid", "PatientId": "guid", "ProductionOrderId": "guid",
  "CatalogPlanId": "guid", "ReleasedAt": "…",
  "DeliveryAddress": "Av. Las Flores #123", "TrackingNumber": "…",
  "Latitude": -12.0464, "Longitude": -77.0428,
  "Id": "guid", "CreatedAt": "…", "CorrelationId": "guid", "Source": "contracting"
}
```

`DeliveryAddress` y `TrackingNumber` salen de la etiqueta del paquete. `Latitude` y
`Longitude` salen del calendario activo del paciente y pueden venir en `null`.

Estado real de MS5 (`nurtricenter-logistics-and-delivery`, rama
`feat/gateway-yarp-and-stuff`, revisado el 2026-10-23):

- **Hoy no consume nada de RabbitMQ**, tampoco `package-validated`. Solo publica sus
  propios exchanges (`courier-created`, `route-created`, `delivery-completed`, …).
- Los paquetes le entran por HTTP: `POST /api/v1/packages/deliveries`, que por
  gateway queda en `/api/nurtricenter/v1/packages/deliveries`.
- Mientras MS5 no tenga consumidor, el mensaje espera en `ms5.package-validated`.

Para que MS5 lo consuma, su consumidor tendría que mapear `PackageValidated` a su
modelo así:

| `PackageValidated` (MS3) | MS5 (`AddDeliveryRequestItem` / `ValidatedPackage`) |
|---|---|
| `PatientId` | `PatientId` |
| `PackageId` | `PackageDetails.PackageId` |
| `DeliveryAddress` | `PackageDetails.Destination.Address` |
| `Latitude` / `Longitude` | `PackageDetails.Destination.AddressCoordinates.Lat` / `.Long` |
| `TrackingNumber` | `LabelData` del `ValidatedPackage` (o `AdditionalDetails`) |

Además debería deduplicar por `Id`, igual que MS3 con el inbox.

`AddressCoordinates` no admite nulos en MS5: si `Latitude`/`Longitude` llegan en
`null` (paciente sin calendario cuya dirección coincida), MS5 tiene que
rechazarlo o geocodificar la dirección.

### Integraciones que todavía no son eventos

| Con | Hoy | Qué esperar cuando exista |
|---|---|---|
| MS1 pacientes | Simulado (`GET /v1/sim/patients/{id}`) | Un evento de paciente registrado/actualizado que MS3 proyecte, igual que los planes |
| MS2 composición de plan (comidas → recetas) | Simulada | El catálogo no la publica; hace falta un evento nuevo o una consulta HTTP con JWT |
| MS4 calendarios activos | Simulados | Un evento de calendario creado/actualizado para armar la orden diaria |
| Identity (`ms3.user-*`) y comidas (`ms3.food-*`) | Las colas existen, MS3 no las consume | Acumulan mensajes; consumirlos cuando producción los necesite |

## Diagnóstico rápido

| Síntoma | Causa | Qué hacer |
|---|---|---|
| Worker en `Restarting`, log `ACCESS_REFUSED … guest` | El JSON de definiciones no declara `users`: al importarlo, RabbitMQ no crea `guest` | Ya corregido en `topology/rabbitmq-definitions.json`; recrear el broker: `docker compose up -d --force-recreate rabbitmq` |
| La fila del outbox queda `Published` pero la cola no recibe nada | Tras reiniciar el broker se perdieron los bindings (los exchanges no son durables) y no se reimportó la topología | Ya corregido: `definitions.skip_if_unchanged = false`. En la UI de RabbitMQ, el exchange debe mostrar su binding a la cola |
| Mensajes encolados desaparecen tras reiniciar RabbitMQ | Joseco 1.1.0 publica mensajes no persistentes | Limitación de la librería, igual que en el catálogo; el consumidor debe estar corriendo |
| `POST /v1/contracts` → 404 `PLAN_NOT_FOUND` con un plan de MS2 | El worker aún no proyectó el plan, o el mensaje no llegó | Revisar `ms3.meal-plan-published` en RabbitMQ y `docker logs` del worker |
| Gateway → 404 en `/api/contracting/...` | Consul aún no ve el servicio sano, o el gateway no refrescó (30 s) | Ver `http://localhost:8500`; esperar medio minuto |
| GET devuelve 400 "input does not contain any JSON tokens" | Se envió `Content-Type: application/json` en un GET sin cuerpo | No enviar ese header en los GET |
| La validación de paquetes devuelve 500 | Algún paquete de la orden no está `Labeled` (excepción de dominio sin mapear) | Validar órdenes cuyos paquetes estén todos etiquetados; el script usa una orden nueva |

## Resultados de la verificación (2026-10-23)

| Prueba | Resultado |
|---|---|
| `dotnet test` de la solución | 167 pruebas OK |
| Standalone: plan y receta de MS2 → proyecciones | OK; duplicado con el mismo `Id` descartado |
| Standalone: pago → `ms4.contract-activated` | OK |
| Standalone: validación de paquete → `ms5.package-validated` | OK |
| Broker caído durante el pago | La fila quedó `Simulated` y se publicó sola ~16 s después de levantar RabbitMQ; los consumidores se reconectaron |
| Reinicio del broker | Antes de la corrección se perdían los bindings; después de la corrección se reimportan los 5 bindings |
| Plataforma: registro en Consul como `contracting` | OK |
| Plataforma: `demo-eventos.ps1` vía gateway (`:15005/api/contracting`) | OK de punta a punta; `PackageValidated` llega con dirección, tracking y coordenadas |
