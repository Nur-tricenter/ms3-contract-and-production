# Hoja de ruta: MS3 integrado con el equipo

Qué está hecho, qué falta, quién lo hace y cómo probarlo mientras falta MS1.
Estado al 2026-10-24.

- Diseño de la integración: [INTEGRACION_PLATAFORMA.md](INTEGRACION_PLATAFORMA.md).
- Cómo probar y qué trae cada llamada y evento: [PRUEBAS_INTEGRACION.md](PRUEBAS_INTEGRACION.md).
- Reglas del equipo: repo `knowledge` (`CONTRIBUTING.md`, `contracts/README.md`).

## 1. Qué hay en el workspace

Todo vive en `C:\jesusferru\studies\diplomado\`.

| Carpeta | Repo | Rol | Rama de trabajo |
|---|---|---|---|
| `ms3-contract-and-production` | `Nur-tricenter/ms3-contract-and-production` | **MS3** contratación y producción (tuyo) | `feat/ms4-delivery-calendar-rest` |
| `nur-tricenter-local` | `Nur-tricenter/nur-tricenter-local` | Orquestador: compose, topología, bases, service accounts | `feat/ms3-contracting` |
| `nur-tricenter-shared-kernels` | `Nur-tricenter/nur-tricenter-shared-kernels` | Kernels NuGet; aquí van los contratos de eventos | `feat/contracting-messages` |
| `knowledge` | `Nur-tricenter/knowledge` | Fichas de cada servicio y catálogo de eventos | — (falta la ficha de MS3) |
| `ms4-delivery-schedule-management` | `Nur-tricenter/ms4-delivery-schedule-management` | **MS4** calendario de entregas | `main` (sin cambios) |
| `ms5-logistics-and-delivery` | `Nur-tricenter/ms5-logistics-and-delivery` | **MS5** logística | `main` (sin cambios) |
| `ms2-plans-and-recipes-catalog` | `Nur-tricenter/ms2-plans-and-recipes-catalog` | **MS2** catálogo | `main` (sin cambios) |
| `nur-tricenter-identity` | `Nur-tricenter/nur-tricenter-identity` | Identity (JWT, usuarios) | `main` (sin cambios) |
| `nur-tricenter-api-gateway` | `Nur-tricenter/nur-tricenter-api-gateway` | YARP + Consul | `main` (sin cambios) |

MS1 (pacientes) todavía no existe en la organización.

## 2. Cómo se comunica MS3 y qué cambia en cada repo

Criterio: cada servicio es dueño de sus datos y de sus hechos. Se usa REST cuando el
otro servicio ya diseñó un endpoint para eso (MS4), y eventos cuando se anuncia un
hecho que otros toman cuando quieran (MS2 → MS3, MS3 → MS5).

| Repo | Cambio | Por qué es lo mínimo |
|---|---|---|
| MS3 | Outbox → RabbitMQ (MS5), proyecciones de MS2, REST a MS4 con `ms3-service`, Consul | MS3 no decide cómo agenda MS4 ni cómo rutea MS5: solo pide el calendario y anuncia el paquete |
| shared-kernels | `Messages/Contracting/PackageValidated` y versión 1.2.0 | Solo vocabulario, cero lógica. MS3 → MS4 es REST, así que no lleva contrato de evento |
| nur-tricenter-local | Incluir MS3, clonarlo en `setup-project`, compilarlo y crear `ms3-service` en `start-stack`; exchange y cola de `package-validated`; base de MS3; reimportar la topología en cada arranque | Mismo patrón que siguieron MS4 y MS5 |
| gateway | **Nada** | Las rutas salen de Consul: `contracting` → `/api/contracting/*` |
| MS4 | **Nada** | MS3 usa los endpoints que MS4 ya expone (`/schedule`, `/active-tomorrow`) con los scopes que MS4 ya exige |
| identity | **Nada** | La cuenta `ms3-service` se crea con el endpoint existente de usuarios |
| MS2 | **Nada obligatorio** | Opcional: `Price` en `MealPlanPublished` (hoy MS3 proyecta costo 0) |
| MS5 | Consumidor de `ms5.package-validated` (su equipo) | Hoy recibe paquetes por un POST anónimo |

## 3. Pasos en orden

### Fase A — Tu PC (tú)

| # | Qué | Estado | Cómo |
|---|---|---|---|
| A1 | Docker Desktop | Listo | `docker version` |
| A2 | .NET SDK | Listo (10.0.401 compila net8 y net10) | `dotnet --list-sdks` |
| A3 | Acceso a la org `Nur-tricenter` | Listo | MS3 ya está subido |
| A4 | Token de GitHub (classic) con `read:packages` | **Pendiente** | GitHub → Settings → Developer settings → Personal access tokens (classic) |
| A5 | Variables de entorno del token | **Pendiente** | Ver abajo; luego cerrar y abrir la terminal |
| A6 | `.env` de MS5 con la conexión a Neon | Opcional | Pedírselo al dueño de MS5; sin él, MS5 arranca pero falla en su primera consulta |
| A7 | `gh` CLI (opcional) | No instalado | `winget install GitHub.cli` para abrir PRs desde la terminal |

```powershell
[Environment]::SetEnvironmentVariable("NUGET_USERNAME", "tu-usuario-github", "User")
[Environment]::SetEnvironmentVariable("NUGET_TOKEN",   "tu-token",          "User")
```

Prueba de que el token sirve:

```bash
dotnet restore ../ms4-delivery-schedule-management/src/Api/Api.csproj
```

Si no da 401, el token sirve.

### Fase B — Commits y PRs (tú apruebas, yo ejecuto)

| Repo | Rama | Commit |
|---|---|---|
| MS3 | `feat/ms4-delivery-calendar-rest` | `feat: call MS4 schedule and active-tomorrow through the gateway with ms3-service` |
| nur-tricenter-local | `feat/ms3-contracting` | `feat: add ms3 contracting to the local stack` |
| shared-kernels | `feat/contracting-messages` | `feat: add contracting PackageValidated message` |
| knowledge | `ms3/context` | `docs(ms3): add MS3 contracting context` (después de los anteriores) |

Cada rama se sube con `git push -u origin <rama>` y entra por PR, con revisión de otro
integrante (regla del equipo).

### Fase C — Coordinación con el equipo (tú, con cada dueño)

1. **Dueño del kernel:** mergear `feat/contracting-messages` y publicar
   `Nur.Tricenter.Integration 1.2.0`. Publicar necesita `write:packages`.
2. **Dueño de `nur-tricenter-local`:** mergear `feat/ms3-contracting`. Avisarle del
   arreglo de RabbitMQ: protege a todos los consumidores, no solo a MS3.
3. **Dueño de MS4:** avisarle que MS3 ya llama a `/schedule` y `/active-tomorrow`
   como `ms3-service`. Su gap `ms4-gap-04` (estado `InProduction`) sigue abierto:
   MS3 podría avisar "empecé a producir" si acuerdan el endpoint.
4. **Equipo de MS5:** consumidor de `ms5.package-validated`. La tabla de mapeo de
   campos está en PRUEBAS_INTEGRACION.md.
5. **Equipo de MS2 (opcional):** precio del plan en `MealPlanPublished`.

### Fase D — Cuando haya token y kernel 1.2.0 (yo, cuando me digas)

1. MS3 cambia sus copias locales por `Nur.Tricenter.Integration 1.2.0`:
   - `NuGet.config` igual al del catálogo, con `Nur.Tricenter.*` mapeado al feed de la org.
   - Borrar `Infrastructure/Messaging/Contracts/`.
2. Levantar la plataforma completa con `start-stack.ps1` (catálogo, identity, MS4,
   MS5 y MS3), y correr `resolve-daily-catalog` de MS4 para que `active-tomorrow`
   tenga datos.
3. Repetir los casos de uso de abajo con datos reales.
4. Cargar la ficha de MS3 en `knowledge` y validarla con `npm run validate`.

## 4. Casos de uso

Cómo se cubre la ausencia o el estado de cada servicio:

- **MS1:** se simula con 3 pacientes deterministas (`00000000-…-0001` Juan Perez,
  `…-0002` Juana Suarez, `…-0003` Luis Ferrufino).
- **MS2 sin token:** se publica a mano en sus exchanges con la misma forma JSON que
  usa el catálogo real.
- **MS4:**
  - En la plataforma con token, es el real.
  - Sin token, o en standalone, los calendarios activos son los simulados y
    `CreateSchedule` queda `Unroutable` (standalone) o pendiente (plataforma sin MS4).
- **MS5:** como aún no consume, el mensaje se ve en `ms5.package-validated`.

Base de las rutas:

- MS3 solo: `http://localhost:8080/api`.
- Plataforma: `http://localhost:15005/api/contracting`.

`scripts/demo-eventos.ps1` ejecuta CU1, CU4 y CU5 seguidos.

| # | Caso de uso | Pasos | Resultado esperado |
|---|---|---|---|
| CU1 | Un plan publicado por MS2 se puede contratar | Publicar `MealPlanPublished` en `meal-plan-published`; luego `POST /v1/contracts` con ese `PlanId` y un paciente simulado | 201 `PendingPayment`; fila en `CatalogPlans` y en `InboxMessages` |
| CU2 | Mensaje duplicado | Publicar dos veces el mismo `MealPlanPublished` (mismo `Id`) con otro `Name` | Se aplica solo el primero; log `Skipping … already in the inbox` |
| CU3 | Receta de MS2 en la orden de producción | Publicar `RecipePublished` con `RecipeId` `20000000-0000-0000-0000-000000000001`; luego `GET /v1/production/daily-order?productionDate=<fecha nueva>` (standalone) | Esa receta aparece con el nombre publicado |
| CU4 | Pago → calendario en MS4 | `POST /v1/billing/charge-plan` del contrato de CU1 (plan de 30 días) | 200 `processed`; en ~5 s la fila `CreateSchedule` pasa a `Published` (`GET /v1/sim/outgoing/CreateSchedule`); MS4 tiene el calendario con inicio al día siguiente |
| CU5 | Paquete validado → MS5 | Orden diaria → crear paquete → `assemble` → `label` → `validate` con `totalValidatedCount: 1` | 200; llega `PackageValidated` a `ms5.package-validated` con dirección, tracking y coordenadas |
| CU6 | MS4 rechaza el calendario | Publicar un plan de 7 días, contratarlo y pagarlo | La fila `CreateSchedule` queda `Rejected` (MS4 solo acepta 15 o 30) y no se reintenta |
| CU7 | MS4 o identity caídos | Detener MS4 (o identity) y hacer CU4; luego levantarlo | La fila queda `Simulated` y pasa sola a `Published` cuando MS4 vuelve; el pago respondió 200 igual |
| CU8 | Broker caído | `docker stop` del RabbitMQ; hacer CU5; `docker start` | La fila `TransferToLogistics` pasa sola a `Published` y el mensaje llega a la cola |
| CU9 | Plan desconocido | `POST /v1/contracts` con un `catalogPlanId` cualquiera | 404 `PLAN_NOT_FOUND` |
| CU10 | Paciente desconocido (MS1 ausente) | `POST /v1/contracts` con un `patientId` fuera de los 3 simulados | 404 `PATIENT_NOT_FOUND` |
| CU11 | Descubrimiento por gateway | Abrir `http://localhost:8500` y llamar a `/api/contracting/v1/sim/patients/00000000-0000-0000-0000-000000000001` en `:15005` | Servicio `contracting` en verde; 200 desde el gateway |
| CU12 | Reinicio del broker no rompe el ruteo | `docker restart` del RabbitMQ y repetir CU5 | Los bindings se reimportan y el mensaje llega |
| CU13 | (Fase D) Catálogo real | `start-stack.ps1` con token | Los planes y recetas sembrados por MS2 aparecen solos en MS3; contratar uno de 15 o 30 días y ver su calendario en MS4 |

Consultas útiles a la base de MS3:

- Plataforma: `docker exec -it nur-tricenter-postgres psql -U dev-only -d nur_tricenter_contracting`.
- MS3 solo: el contenedor `postgres` del compose standalone.

```sql
select "PlanId","Name","TotalDays" from "CatalogPlans";
select "RecipeId","Name" from "CatalogRecipes";
select "MessageType","ProcessedOn" from "InboxMessages" order by "ProcessedOn" desc;
select "EventType","Status","PublishedAt" from "OutgoingIntegrationEvents" order by "CreatedAt" desc;
```
