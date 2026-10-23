---
name: test-flows
description: Flujos de negocio del microservicio Nurtricenter MS3 con sus rutas, payloads, precondiciones y códigos de respuesta. Úsalo al escribir o revisar pruebas de integración, al necesitar identificadores de simulación válidos, o al determinar qué flujo correcto o incorrecto conviene cubrir.
---

# Flujos de negocio — Nurtricenter MS3

MS3 gestiona contratación, facturación y producción diaria. Todos los endpoints
son `AllowAnonymous()`, así que no hace falta autenticación.

## Identificadores de simulación

`SimulationDataService` es determinista: los mismos GUID siempre devuelven los
mismos datos. No inventes GUIDs, usa estos.

**Pacientes** (simulan MS1)

| GUID | Nombre |
|---|---|
| `00000000-0000-0000-0000-000000000001` | Juan Perez |
| `00000000-0000-0000-0000-000000000002` | Juana Suarez |
| `00000000-0000-0000-0000-000000000003` | Luis Ferrufino |

**Planes** (simulan MS2)

| GUID | Nombre | Precio | Días |
|---|---|---|---|
| `10000000-0000-0000-0000-000000000001` | Premium Month Plan | 550.00 | 30 |
| `10000000-0000-0000-0000-000000000002` | Basic Month Plan | 350.00 | 30 |
| `10000000-0000-0000-0000-000000000003` | Standard Plan | 150.00 | 15 |

Un GUID aleatorio (`Guid.NewGuid()`) **no** existe para la simulación: es la
forma correcta de provocar los caminos de "no encontrado".

## Máquina de estados

```
Contract:  PendingPayment ──(charge-plan)──> Active ──(cancel)──> Canceled
Package:   Pending ──(assemble)──> Assembled ──(label)──> Labeled ──(validate)──> Validated
```

Una transición fuera de orden lanza `InvalidOperationException`, que FastEndpoints
traduce a **500**. Úsalas para cubrir el flujo incorrecto.

## Flujo correcto — ciclo de vida del contrato

Recorre los tres subdominios encadenando el contrato recién creado.

| # | Petición | Esperado |
|---|---|---|
| 1 | `POST /api/v1/contracts` `{patientId, catalogPlanId}` | **201**, `status: "PendingPayment"`, cabecera `Location`. Guardar `id` |
| 2 | `GET /api/v1/contracts/{id}` | **200**, `status: "PendingPayment"` |
| 3 | `POST /api/v1/billing/charge-plan` `{contractId: id, amount, invoiceNumber}` | **200**, `status: "processed"`. Requiere contrato en `PendingPayment` |
| 4 | `GET /api/v1/billing/{invoiceNumber}` | **200**, `isPaid: true`, `planName` resuelto desde MS2 |
| 5 | `PUT /api/v1/contracts/{id}/cancel` `{reason}` | **204**. Requiere contrato en `Active` |

El paso 3 además escribe un `OutgoingIntegrationEvent` de tipo `CreateSchedule`
dirigido a MS4, consultable en `GET /api/v1/sim/outgoing/CreateSchedule`. Ese es
el efecto colateral que conviene verificar con `QueryDbAsync`.

## Flujo correcto — ciclo de vida del paquete

| # | Petición | Esperado |
|---|---|---|
| 1 | `POST /api/v1/production/packages` `{productionOrderId, patientId, catalogPlanId}` | **201**, `status: "Pending"`. Guardar `packageId` |
| 2 | `POST /api/v1/production/packages/assemble` `{packageId, staffId}` | **204** → `Assembled` |
| 3 | `POST /api/v1/production/packages/label` `{packageId, patientId}` | **204** → `Labeled` |
| 4 | `POST /api/v1/production/packages/validate` `{productionOrderId, supervisorId, batchValidated: true, totalValidatedCount: 0}` | **200**, `validatedCount` igual al número de paquetes |

`productionOrderId` puede ser cualquier GUID no vacío si no se usa el endpoint de
orden diaria; el paquete no valida contra la orden.

`totalValidatedCount: 0` significa "no verificar el conteo". Un valor distinto de
cero que no coincida con la cantidad real devuelve **400**.

El paso 4 escribe un evento `TransferToLogistics` hacia MS5.

## Flujo incorrecto — caminos de error

Estos son los que la rúbrica espera. Todos son alcanzables sin datos previos,
salvo los marcados como dependientes.

| Escenario | Petición | Esperado |
|---|---|---|
| Paciente inexistente | `POST /contracts` con `patientId: Guid.NewGuid()` y plan válido | **404** |
| Plan inexistente | `POST /contracts` con `patientId` válido y `catalogPlanId: Guid.NewGuid()` | **404** |
| Contrato inexistente | `GET /contracts/{Guid.NewGuid()}` | **404** |
| Pago sobre contrato inexistente | `POST /billing/charge-plan` con `contractId: Guid.NewGuid()` | **404** |
| Factura inexistente | `GET /billing/FACTURA-QUE-NO-EXISTE` | **404** |
| Paquete inexistente | `POST /production/packages/assemble` con `packageId: Guid.NewGuid()` | **404** |
| Orden de producción sin paquetes | `POST /production/packages/validate` con `productionOrderId: Guid.NewGuid()` | **404** |
| Batch no confirmado | `POST /production/packages/validate` con `batchValidated: false` (requiere paquetes) | **400** |
| Conteo inconsistente | `POST /production/packages/validate` con `totalValidatedCount` que no coincide (requiere paquetes) | **400** |
| Pago duplicado | `charge-plan` dos veces sobre el mismo contrato | **500** — la segunda vez el contrato ya está `Active` |
| Cancelar sin pagar | `PUT /contracts/{id}/cancel` sobre un contrato `PendingPayment` | **500** |
| Copago con contrato activo | `POST /billing/charge-control` para un paciente que ya tiene contrato `Active` | **409** |

El caso de **copago con contrato activo** es el más valioso del conjunto: es el
único error de negocio que devuelve **409 Conflict**, y exige encadenar dos
flujos (crear contrato → pagarlo → cobrar el copago) para llegar a él.

## Verificación de estado

Un código HTTP correcto no prueba que el cambio se haya persistido. Acompaña cada
flujo con una consulta:

```csharp
var status = await factory.QueryDbAsync(async db =>
    (await db.Contracts.FindAsync(contractId))!.Status);
```

## Endpoints de simulación

No tocan la base, así que no necesitan `SeedAsync`. Útiles para probar el arranque:

```
GET  /health
GET  /api/v1/sim/patients/{id:guid}
GET  /api/v1/sim/catalog/plans/{id:guid}
POST /api/v1/sim/catalog/plans/structure        {catalogPlanIds: [guid]}
GET  /api/v1/sim/calendars/active-tomorrow
GET  /api/v1/sim/outgoing/{eventType}
```
