# MS3 en la plataforma Nur-Tricenter

Cómo se conecta MS3 (Contratación y Producción) con el resto de la plataforma que
armó el equipo en la organización `Nur-tricenter`: `nur-tricenter-local`
(orquestador), `nur-tricenter-api-gateway` (YARP + Consul),
`ms2-plans-and-recipes-catalog` (MS2), `nur-tricenter-identity` y
`nur-tricenter-shared-kernels`.

## Qué se copió de la plataforma

| Pieza de la plataforma | En MS3 |
|---|---|
| Transporte `Joseco.Communication.External.RabbitMQ` 1.1.0 | `Nurtricenter.MS3.WorkerService` (publicador y consumidores) |
| Exchanges `fanout`, no durables, una cola por consumidor `msN.<exchange>` | `contract-activated`, `package-validated`; colas `ms3.*` del catálogo |
| Proceso API + proceso Worker | `Nurtricenter.Api` + `Nurtricenter.MS3.WorkerService` |
| Outbox transaccional drenado por el worker | Tabla `OutgoingIntegrationEvents` + `OutboxRelay` |
| Inbox idempotente (claim + proyección en un solo `SaveChanges`) | `IdempotentConsumer<T, TInner>` + tabla `InboxMessages` |
| Auto-registro en Consul con check HTTP a `/health` | `Infrastructure/Discovery/ServiceRegistration` |
| Fragmento compose de plataforma + compose standalone | `contracting.compose.yml` + `docker-compose.yml` |
| Flags `Migrations__ApplyOnStartup`, `Seed__Enabled`, `Swagger__Enabled` | `Program.cs` de la API (por defecto siguen a `IsDevelopment()`) |

## Mensajes

### MS3 consume (de MS2 — catálogo)

| Cola | Exchange | Contrato | Efecto en MS3 |
|---|---|---|---|
| `ms3.meal-plan-published` | `meal-plan-published` | `MealPlanPublished` | Upsert en `CatalogPlans` |
| `ms3.recipe-published` | `recipe-published` | `RecipePublished` | Upsert en `CatalogRecipes` |
| `ms3.recipe-updated` | `recipe-updated` | `RecipeUpdated` | Upsert en `CatalogRecipes` |

Las proyecciones se usan a través de `CatalogProjectionSimulationService`, que
decora `ISimulationService`:

- `GetPlan`: primero la simulación determinista, si no existe, el plan
  proyectado. Así `POST /api/v1/contracts` acepta planes reales de MS2.
- `GetRecipeName`: nombre real de la receta en la orden de producción diaria; si
  no se conoce, se mantiene `Recipe-xxxxxxxx`.

### MS3 publica

| Evento interno (`EventType`) | Exchange | Contrato | Cola destino |
|---|---|---|---|
| `CreateSchedule` (al pagar un contrato) | `contract-activated` | `ContractActivated` | `ms4.contract-activated` |
| `TransferToLogistics` (al aprobar calidad de un paquete) | `package-validated` | `PackageValidated` | `ms5.package-validated` |

Ejemplo de cuerpo publicado (`ContractActivated`):

```json
{
  "EventType": "Contract.Activated",
  "ContractId": "6c0f…",
  "PatientId": "00000000-0000-0000-0000-000000000001",
  "CatalogPlanId": "10000000-0000-0000-0000-000000000001",
  "InvoiceNumber": "inv-2026-0500",
  "TotalAmount": 550.00,
  "Id": "<id de la fila del outbox>",
  "CreatedAt": "2026-10-23T12:00:00+00:00",
  "CorrelationId": "<id de la fila del outbox>",
  "Source": "contracting"
}
```

El `Id` del mensaje es el `Id` de la fila del outbox: si el worker se cae entre
publicar y marcar la fila, la republicación lleva el mismo `Id` y el inbox del
consumidor la descarta.

## Ciclo de vida de una fila del outbox

Los handlers no cambiaron: siguen guardando la fila con estado `Simulated`. Ese
estado ahora significa *pendiente de publicar* (`OutgoingIntegrationEvent.PendingStatus`).

```
Simulated ──OutboxRelay publica──▶ Published (PublishedAt)
    │
    └── tipo sin exchange o payload inválido ──▶ Unroutable
```

Si RabbitMQ falla, la fila queda en `Simulated` y se reintenta en el siguiente
ciclo (`Outbox__PollingIntervalSeconds`, 5 s por defecto).
`GET /api/v1/sim/outgoing/{eventType}` sigue sirviendo para inspeccionar las filas.

## Cómo levantarlo

### Dentro de la plataforma

Requiere que `dipl-arqui-microservicios` esté al lado de `nur-tricenter-local`
(ya es así en este workspace) y la rama `feat/ms3-contracting` de
`nur-tricenter-local`.

```bash
cd ../nur-tricenter-local && pwsh ./scripts/start-stack.ps1
```

- Gateway: `http://localhost:15005/api/contracting/v1/contracts/{id}`
- Swagger de MS3 (solo con `start-stack-dev`): `http://localhost:18083/swagger`
- RabbitMQ: `http://localhost:15672` (guest / guest)

El catálogo y identity dependen de los kernels privados `Nur.Tricenter.*` (GitHub
Packages). Sin `NUGET_TOKEN` no compilan; MS3 y el gateway sí.

### Solo MS3 (standalone)

```bash
docker compose up --build
```

Levanta Postgres (`5434`), RabbitMQ (`5672`/`15672`) con la topología de
`topology/`, la API en `8080` (Swagger encendido) y el worker. Para simular a MS2,
publicar desde la UI de RabbitMQ en `meal-plan-published` un JSON con la forma de
`MealPlanPublished`.

### Local sin Docker

Copiar `appsettings.Development.template.json` a `appsettings.Development.json`
en `Nurtricenter.Api` y en `Nurtricenter.MS3.WorkerService`, ajustar cadenas de
conexión y correr ambos con `dotnet run`.

## Decisiones y pendientes

- **Contratos locales en vez de `Nur.Tricenter.Integration`.** El feed de GitHub
  Packages devuelve 401 sin token, y agregar el paquete rompería el build y las
  pruebas de MS3. Se verificó que el publicador de Joseco serializa sin `$type`
  (Newtonsoft con `TypeNameHandling.Auto` y el tipo concreto), así que basta con
  que los nombres de propiedades coincidan. Cuando haya token, los contratos de
  `Infrastructure/Messaging/Contracts/` deben pasar al kernel
  (`Messages/Contracting/`, `Messages/Production/`) y los espejos del catálogo
  reemplazarse por los del kernel.
- **`MealPlanPublished` no trae precio.** El plan proyectado se expone con
  `Cost = 0`; hoy ningún handler usa `Cost` (el monto llega en el pago). Si MS3
  tiene que validar montos, MS2 debe agregar el precio al contrato.
- **Estructura de planes y calendarios activos siguen simulados.** El catálogo no
  publica la composición del plan (comidas → recetas) ni existe aún MS4.
- **Colas `ms3.food-*` y `ms3.user-*`** existen en la topología de la plataforma
  pero MS3 no las consume todavía; acumulan mensajes.
- **Autenticación.** Los endpoints de MS3 siguen `AllowAnonymous()`. El catálogo ya
  valida JWT de identity (`Nur.Tricenter.Security`); MS3 debería hacer lo mismo
  cuando tenga acceso a los kernels.
