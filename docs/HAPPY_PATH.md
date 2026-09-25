# Happy Path — Nurtricenter MS3

Guía del flujo completo y feliz del microservicio: desde que un paciente
contrata un plan hasta que su paquete sale validado hacia logística. Todos los
endpoints son `AllowAnonymous()`, no hace falta autenticación.

Los identificadores de simulación (`patientId`, `catalogPlanId`) son
deterministas — ver `Nurtricenter.MS3.Application/Simulations/`. Los mismos
GUID siempre devuelven los mismos datos simulados de MS1 (pacientes) y MS2
(planes).

## 1. Contratación y facturación

```
1. POST /api/v1/contracts                    → 201, status: "PendingPayment"
2. GET  /api/v1/contracts/{id}                → 200, status: "PendingPayment"
3. POST /api/v1/billing/charge-plan           → 200, status: "processed"
4. GET  /api/v1/billing/{invoiceNumber}       → 200, isPaid: true
5. PUT  /api/v1/contracts/{id}/cancel         → 204 (opcional, cierra el ciclo)
```

| Paso | Qué pasa |
|---|---|
| 1 | Se crea el contrato entre `patientId` y `catalogPlanId`. MS3 valida ambos contra las simulaciones de MS1/MS2 antes de crearlo. |
| 3 | El pago activa el contrato (`PendingPayment` → `Active`), emite la factura y registra un evento saliente `CreateSchedule` hacia MS4 — consultable en `GET /api/v1/sim/outgoing/CreateSchedule`. |
| 5 | Solo un contrato `Active` puede cancelarse. Cancelar uno `PendingPayment` lanza `InvalidOperationException` (500). |

Alternativa sin plan: `POST /api/v1/billing/charge-control` cobra un control de
seguimiento (`ControlCharge`) sin pasar por un contrato. Si el paciente ya tiene
un contrato `Active`, el copago devuelve **409 Conflict** — es la única regla de
negocio del microservicio que responde con ese código.

## 2. Producción diaria y empaquetado

```
1. GET  /api/v1/production/daily-order                → 200, consolida la orden del día
2. POST /api/v1/production/packages                    → 201, status: "Pending"
3. POST /api/v1/production/packages/assemble            → 204 → "Assembled"
4. POST /api/v1/production/packages/label                → 204 → "Labeled"
5. POST /api/v1/production/packages/validate              → 200, validatedCount
```

Máquina de estados del paquete:

```
Pending ──(assemble)──> Assembled ──(label)──> Labeled ──(validate)──> Validated
```

Cualquier transición fuera de orden (por ejemplo, validar un paquete que sigue
`Pending`) lanza `InvalidOperationException`, que FastEndpoints traduce a
**500**. El paso de validación además escribe un evento saliente
`TransferToLogistics` hacia MS5, consultable en
`GET /api/v1/sim/outgoing/TransferToLogistics`.

## 3. Simulaciones de otros microservicios

MS1, MS2, MS4 y MS5 no existen en este repositorio. `Nurtricenter.MS3.Application/Simulations/SimulationDataService`
los simula de forma determinista, y hay endpoints auxiliares para
inspeccionarlos directamente sin pasar por un flujo de negocio:

```
GET  /api/v1/sim/patients/{id}
GET  /api/v1/sim/catalog/plans/{id}
POST /api/v1/sim/catalog/plans/structure
GET  /api/v1/sim/calendars/active-tomorrow
GET  /api/v1/sim/outgoing/{eventType}
```

## Ver también

- Tabla completa de endpoints y diagrama de clases: [`../README.md`](../README.md).
- Flujos correctos e incorrectos con payloads exactos para pruebas de
  integración: skill `test-flows` (`.claude/skills/test-flows/SKILL.md`).
- Cómo correr la API y la suite de pruebas: [`../../CLAUDE.md`](../../CLAUDE.md).
