# Cómo probar la comunicación de MS3 y qué esperar de cada evento

Guía práctica para verificar que MS3 (Contratación y Producción) habla con el resto
de la plataforma: eventos por RabbitMQ con MS2 y MS5, y REST con MS4. Complementa a
[INTEGRACION_PLATAFORMA.md](INTEGRACION_PLATAFORMA.md), que explica el diseño.

Todo lo de esta guía se ejecutó el 2026-10-23 contra contenedores reales (Docker
29.9, RabbitMQ 3.13, PostgreSQL 16, Consul 1.20); los resultados están al final.

## Requisitos

| Qué | Para qué | Cómo verificar |
|---|---|---|
| Docker Desktop corriendo | Postgres, RabbitMQ, Consul y los contenedores de MS3 | `docker version` muestra Client y Server |
| .NET SDK 8 (y 10 para el gateway) | Compilar MS3 y el gateway antes de levantar la plataforma | `dotnet --list-sdks` |
| Puertos libres | 5672/15672 (RabbitMQ), 8080 o 15005 (API/gateway), 5433/5434 (Postgres), 8500 (Consul) | `docker ps` sin otro stack levantado |
| `NUGET_TOKEN` | Compilar catálogo, identity y MS4 reales (MS3 no lo necesita) | `dotnet restore` del catálogo sin 401 |

Los dos stacks usan los mismos puertos de RabbitMQ: levanta uno a la vez.

## Nivel 1 — Pruebas automáticas (sin Docker)

```bash
dotnet test Nurtricenter.MS3.sln
```

Las pruebas de `Nurtricenter.MS3.IntegrationTests/Messaging/` cubren la integración
sin broker ni MS4: el relay publica en un `BrokerEnMemoria`, MS4 es un
`CalendarioEnMemoria` y la base es SQLite in-memory.

| Clase | Qué demuestra |
|---|---|
| `OutboxRelayTests` | `CreateSchedule` → `POST /schedule` a MS4 con duración e inicio; MS4 rechaza o falta duración → `Rejected`; MS4 caído → pendiente; MS4 sin configurar → `Unroutable`; `TransferToLogistics` → `PackageValidated` en `package-validated`; no republica; tipo desconocido o payload inválido → `Unroutable`; broker caído → la fila queda pendiente |
| `CatalogConsumersTests` | Proyección de planes y recetas; redelivery con el mismo `Id` se descarta (inbox); deserializa el JSON exacto que publica el catálogo |
| `DeliveryCalendarClientTests` | Cliente HTTP real contra un MS4 e identity falsos: login de `ms3-service`, `Bearer`, JSON exacto de `POST /schedule`, 400/422 → rechazo, 5xx → reintento, mapeo de `active-tomorrow` |
| `CatalogProjectionFlowTests` | Por HTTP: contrato con un plan que solo existe en la proyección → 201; pago → el relay crea el calendario en MS4 con la duración del plan proyectado y el día siguiente como inicio |

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

Stack completo, con catálogo, identity y MS4 (necesita `NUGET_TOKEN`). `start-stack`
crea además la service account `ms3-service`:

```bash
cd ../nur-tricenter-local
pwsh ./scripts/start-stack.ps1
```

Sin token: solo infraestructura, gateway y MS3. Sin identity ni MS4, `CreateSchedule`
queda pendiente y la orden diaria falla al consultar `active-tomorrow`.

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
4. Espera a que el worker drene el outbox y revisa:
   - **MS4:** el estado de la fila `CreateSchedule` (`GET /v1/sim/outgoing/CreateSchedule`).
     `Published` = MS4 creó el calendario; `Unroutable` = sin MS4 (standalone);
     `Rejected` = MS4 lo rechazó; pendiente = MS4 o identity no responden.
   - **MS5:** lee la cola `ms5.package-validated` sin consumir el mensaje
     (`ack_requeue_true`).
5. Imprime el resultado y termina con `OK`, o con código 1 si algo falló.

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

### Llamadas REST de MS3 a MS4 (calendario de entregas)

Las dos van por el gateway con el JWT de la service account `ms3-service`, que
`start-stack` crea con los scopes `deliveryCalendar.calendar:create` y
`deliveryCalendar.productionOrder:read`.

#### `POST /api/delivery-calendar/v1/schedule`

- **Cuándo:** al cobrar un contrato (`POST /v1/billing/charge-plan`), que pasa de
  `PendingPayment` a `Active`. La llamada sale del outbox (fila `CreateSchedule`),
  así que un MS4 caído no hace fallar el pago.
- **Qué envía:**

```json
{ "contractId": "guid", "patientId": "guid", "planId": "guid", "duration": 30, "startDate": "2026-10-25" }
```

- `duration` es la duración del plan (simulado, o `TotalDays` del plan proyectado
  desde MS2); `startDate` es el día siguiente al pago.
- **Qué esperar:**

| MS4 responde | Fila `CreateSchedule` | Cómo verlo |
|---|---|---|
| 200 | `Published` | `GET /v1/sim/outgoing/CreateSchedule` |
| 400 / 422 (p. ej. plan de 7 días: MS4 solo acepta 15 o 30) | `Rejected`, sin reintentos | Igual; log `rejected by MS4` en el worker |
| 401, 5xx o sin respuesta | sigue `Simulated` y se reintenta cada 5 s | Log `Outbox relay iteration failed` |
| MS4 sin configurar (standalone) | `Unroutable` | Igual |

Como MS4 es idempotente por `contractId`, un reintento no crea un segundo calendario.

#### `GET /api/delivery-calendar/v1/calendars/active-tomorrow?date=`

- **Cuándo:** al pedir la orden diaria (`GET /v1/production/daily-order`), al
  generar la etiqueta y al validar paquetes (para las coordenadas).
- **Qué devuelve MS4:**

```json
[ { "packId": 101, "patientId": "guid", "planId": "guid", "hour": "10:30:00", "address": "Av. Banzer 123, Santa Cruz", "latitude": -17.78, "longitude": -63.18 } ]
```

- Si el job de MS4 (`resolve-daily-catalog`) no corrió, devuelve `[]` y MS3
  responde `NO_CALENDARS`. Si MS4 no responde, la orden diaria falla.

### Eventos que MS3 publica

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

Estado real de MS5 (`Nur-tricenter/ms5-logistics-and-delivery`, revisado el 2026-10-24):

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

### Integraciones que siguen simuladas

| Con | Hoy | Qué esperar cuando exista |
|---|---|---|
| MS1 pacientes | Simulado (`GET /v1/sim/patients/{id}`) | Un evento de paciente registrado/actualizado que MS3 proyecte, igual que los planes |
| MS2 composición de plan (comidas → recetas) | Simulada | El catálogo no la publica; MS4 ya la resuelve por REST, así que MS3 podría tomarla de MS4 o de MS2 con JWT |
| MS4 sin configurar (standalone) | Calendarios simulados | Con `DeliveryCalendar__BaseUrl` se usan los reales de `active-tomorrow` |
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
| Fila `CreateSchedule` pendiente para siempre | Identity o MS4 no están levantados, o `ms3-service` no existe | Correr `start-stack` (crea la cuenta); revisar `docker logs nur-tricenter-contracting-workerservice` |
| Fila `CreateSchedule` en `Rejected` | MS4 respondió 400/422: duración distinta de 15/30 o plan no proyectado en MS4 | Usar un plan de 15 o 30 días; el log del worker trae el detalle de MS4 |
| Orden diaria con error 500 en la plataforma | MS4 no responde a `active-tomorrow` | Levantar MS4; sin MS4, probar en standalone |

## Resultados de la verificación (2026-10-23 y 2026-10-24)

| Prueba | Resultado |
|---|---|
| `dotnet test` de la solución | 185 pruebas OK |
| Standalone: plan y receta de MS2 → proyecciones | OK; duplicado con el mismo `Id` descartado |
| Standalone: validación de paquete → `ms5.package-validated` | OK |
| Broker caído durante el pago | La fila quedó `Simulated` y se publicó sola ~16 s después de levantar RabbitMQ; los consumidores se reconectaron |
| Reinicio del broker | Antes de la corrección se perdían los bindings; después de la corrección se reimportan los 5 bindings |
| Plataforma: registro en Consul como `contracting` | OK |
| Plataforma: `demo-eventos.ps1` vía gateway (`:15005/api/contracting`) | OK de punta a punta; `PackageValidated` llega con dirección, tracking y coordenadas |
| MS3 en contenedores contra un MS4 e identity falsos (Node, mismas rutas del gateway) | Login de `ms3-service`; `POST /schedule` con `duration: 30` y `startDate` del día siguiente → `Published`; orden diaria, etiqueta y validación con `active-tomorrow`; `PackageValidated` con las coordenadas de MS4 |
| Plan de MS2 de 7 días | MS4 respondió 422 y la fila quedó `Rejected`, sin reintentos |
| MS4 real | Pendiente: no compila sin `NUGET_TOKEN` (kernel privado) |
