# Hoja de ruta: de MS3 local a MS3 integrado con el equipo

Orden de lo que falta, quién lo hace y cómo probarlo mientras MS1 y MS4 no existen.
Estado al 2026-10-23.

- Diseño de la integración: [INTEGRACION_PLATAFORMA.md](INTEGRACION_PLATAFORMA.md).
- Cómo probar y qué trae cada evento: [PRUEBAS_INTEGRACION.md](PRUEBAS_INTEGRACION.md).

## 1. Qué hay en el workspace

Todo vive en `C:\jesusferru\studies\diplomado\`.

| Carpeta | Repo | Rol | Rama local |
|---|---|---|---|
| `dipl-arqui-microservicios/Nurtricenter` | `JesusFerru/dipl-arqui-microservicios` | **MS3** contratación y producción (tuyo) | `feat/ms3-platform-integration` |
| `nur-tricenter-local` | `Nur-tricenter/nur-tricenter-local` | Orquestador: compose, topología de RabbitMQ, bases | `feat/ms3-contracting` |
| `nur-tricenter-shared-kernels` | `Nur-tricenter/nur-tricenter-shared-kernels` | Kernels NuGet; **aquí van los contratos de eventos** | `feat/contracting-messages` |
| `nur-tricenter-api-gateway` | `Nur-tricenter/nur-tricenter-api-gateway` | YARP + Consul | `main` (sin cambios) |
| `ms2-plans-and-recipes-catalog` | `Nur-tricenter/ms2-plans-and-recipes-catalog` | **MS2** catálogo | `main` (sin cambios) |
| `nur-tricenter-identity` | `Nur-tricenter/nur-tricenter-identity` | Identity (JWT, usuarios) | `main` (sin cambios) |
| `nurtricenter-logistics-and-delivery` | `DruGuerrero/nurtricenter-logistics-and-delivery` | **MS5** logística | `dev`; su integración está en `feat/gateway-yarp-and-stuff` |

No falta clonar nada: la organización solo tiene esos 5 repos. MS1 (pacientes) y MS4
(calendarios) todavía no existen en ningún lado.

## 2. ¿Los eventos van en el shared kernel?

**Sí.** Es la regla del kernel (`nur-tricenter-integration/Messages/<Servicio>/`):
publicador y consumidor compilan contra el mismo tipo y así los contratos no se
desincronizan. Su prueba `KernelHasNoDomainKnowledgeTests` exime explícitamente a los
mensajes de la regla de "sin dominio".

En la rama `feat/contracting-messages` del kernel quedaron listos:

- `Messages/Contracting/ContractActivated.cs` (MS3 → MS4).
- `Messages/Contracting/PackageValidated.cs` (MS3 → MS5).
- Versión subida a `1.1.0` (cambio aditivo = minor, según el README del kernel).

Las 9 pruebas del kernel pasan. Compilar el kernel no necesita token: solo publicarlo.

Mientras ese paquete no se publique, MS3 usa copias locales idénticas en
`Infrastructure/Messaging/Contracts/`. El JSON en el cable es el mismo (verificado),
así que el cambio posterior es solo de referencias, sin efecto en el comportamiento.

## 3. Responsabilidad única: qué cambia en cada repo

Criterio: cada servicio publica **sus** hechos y proyecta los ajenos que necesita.
Nadie llama por HTTP a otro para empujarle trabajo, y nadie consulta la base de otro.

| Repo | Cambio | Por qué es lo mínimo |
|---|---|---|
| MS3 | Ya hecho: outbox → RabbitMQ, proyecciones de MS2, Consul | MS3 solo anuncia "contrato activado" y "paquete validado"; no decide cómo entrega MS4 ni cómo rutea MS5 |
| shared-kernels | 2 contratos nuevos + versión | Solo vocabulario, cero lógica |
| nur-tricenter-local | Incluir MS3, 2 exchanges y 2 colas, base de MS3, scripts; arreglo de reimportar la topología | Es el lugar donde el README del orquestador dice que va la topología de cada servicio |
| gateway | **Nada** | Las rutas salen de Consul: `contracting` → `/api/contracting/*` sin tocar código |
| MS2 catálogo | **Nada obligatorio** | MS3 consume lo que ya publica. Opcional: agregar `Price` a `MealPlanPublished` (hoy MS3 proyecta costo 0) |
| identity | **Nada** | MS3 sigue `AllowAnonymous`. Adoptar JWT es un paso posterior y aparte |
| MS5 logística | Agregar un consumidor de `ms5.package-validated` y su fragmento compose para la plataforma | Hoy solo recibe paquetes por HTTP; con el evento no depende de que MS3 lo llame |
| MS1 / MS4 (futuros) | Publicar sus hechos (paciente registrado, calendario creado) y MS4 consumir `ms4.contract-activated` | Así MS3 deja de simularlos sin acoplarse por HTTP |

Lo que **no** conviene hacer:

- Que MS3 llame por HTTP al `POST /api/v1/packages/deliveries` de MS5. MS3 quedaría
  acoplado a la disponibilidad y al modelo de MS5.
- Copiar contratos de un repo a otro a mano, una vez exista el paquete 1.1.0.

## 4. Pasos en orden

### Fase A — Tu PC (tú)

| # | Qué | Estado | Cómo |
|---|---|---|---|
| A1 | Docker Desktop | Listo | `docker version` |
| A2 | .NET SDK | Listo (10.0.401 compila net8 y net10) | `dotnet --list-sdks` |
| A3 | Ser miembro de la org `Nur-tricenter` en GitHub, con permiso de escritura | **Pendiente**: pedírselo al dueño de la org | Sin esto no puedes abrir PRs desde ramas de la org; la alternativa es hacer fork |
| A4 | Token de GitHub (classic) con `read:packages` | **Pendiente** | GitHub → Settings → Developer settings → Personal access tokens (classic) |
| A5 | Variables de entorno del token | **Pendiente** | Ver abajo; luego cerrar y abrir la terminal |
| A6 | Que el dueño te dé acceso a los paquetes de la org | **Pendiente** si el restore sigue en 401 con token | En GitHub Packages, cada paquete tiene su propio control de acceso |
| A7 | `gh` CLI (opcional) | No instalado | `winget install GitHub.cli` para abrir PRs desde la terminal |

```powershell
[Environment]::SetEnvironmentVariable("NUGET_USERNAME", "tu-usuario-github", "User")
[Environment]::SetEnvironmentVariable("NUGET_TOKEN",   "tu-token",          "User")
```

Prueba de que el token sirve:

```bash
dotnet restore ../ms2-plans-and-recipes-catalog/src/Catalog.Domain/Catalog.Domain.csproj
```

Si no da 401, el token sirve.

### Fase B — Commits locales (tú apruebas, yo ejecuto)

| Repo | Commit | Estado |
|---|---|---|
| MS3 | `627857e` integración con RabbitMQ, Consul y worker | Hecho |
| MS3 | `a198934` PackageValidated enriquecido, arreglos de RabbitMQ, guía y script de demo | Hecho |
| MS3 | `docs(ms3): add integration roadmap` | Pendiente |
| nur-tricenter-local | `feat(platform): add MS3 contracting services, topology and database` | Pendiente |
| nur-tricenter-local | `fix(rabbitmq): re-import definitions on every boot so bindings survive restarts` | Pendiente |
| shared-kernels | `feat(integration): add contracting messages ContractActivated and PackageValidated` | Pendiente |

### Fase C — Coordinación con el equipo (tú, con cada dueño)

En este orden, porque cada paso desbloquea el siguiente:

1. **Dueño del kernel:** revisar y mergear el PR de `feat/contracting-messages`, y
   publicar `Nur.Tricenter.Integration 1.1.0`. Publicar necesita `write:packages`, y
   eso es del dueño.
2. **Dueño de `nur-tricenter-local`:** PR de `feat/ms3-contracting`. Hay que acordar
   dónde vive MS3:
   - Hoy el compose lo incluye desde `../../dipl-arqui-microservicios/Nurtricenter`.
   - Si el equipo prefiere un repo propio en la org (p. ej. `ms3-contracting-production`),
     cambia esa ruta y `setup-project` lo clona.
   - Avísale también del arreglo de RabbitMQ: afecta al catálogo igual que a MS3.
3. **Equipo de MS5:**
   - Consumidor de `ms5.package-validated` (la tabla de mapeo de campos está en
     PRUEBAS_INTEGRACION.md).
   - Fragmento compose para unirse a `nur-tricenter-net`.
   - Hoy usan `ServiceName=nurtricenter`, que no choca con `contracting`.
4. **Equipo de MS2 (opcional):** precio del plan en `MealPlanPublished`.
5. **Equipos de MS1 y MS4, cuando existan:** que MS4 consuma `ms4.contract-activated`
   y que ambos publiquen sus hechos. MS3 los proyectará igual que hoy proyecta a MS2.

Antes de cada PR: push de la rama (`git push -u origin <rama>`) desde el repo
correspondiente.

### Fase D — Cuando haya token y kernel 1.1.0 (yo, cuando me digas)

1. MS3 cambia sus copias locales por `Nur.Tricenter.Integration 1.1.0`. Implica:
   - un `NuGet.config` igual al del catálogo;
   - mapear `Nur.Tricenter.*` al feed de la org;
   - borrar `Messaging/Contracts/`.
2. Levantar la plataforma completa con `start-stack.ps1`. El catálogo real siembra 2
   planes y 25 recetas, que deben aparecer en `CatalogPlans` y `CatalogRecipes` de MS3
   sin publicar nada a mano.
3. Repetir los casos de uso de abajo con datos reales del catálogo.

## 5. Casos de uso para probar sin MS1 ni MS4

Cómo se cubre la ausencia de cada servicio:

- **MS1:** se simula con 3 pacientes deterministas (`00000000-…-0001` Juan Perez,
  `…-0002` Juana Suarez, `…-0003` Luis Ferrufino).
- **MS4:** los calendarios activos están simulados. Para el evento que MS4 recibiría,
  se lee la cola `ms4.contract-activated` en RabbitMQ (UI en `http://localhost:15672`,
  pestaña *Queues* → *Get messages*, o el script de demo).
- **MS2 sin token:** se publica a mano en sus exchanges con la misma forma JSON que
  usa el catálogo real.
- **MS5:** como aún no consume, el mensaje se ve en `ms5.package-validated`.

Base de las rutas:

- MS3 solo: `http://localhost:8080/api`.
- Plataforma: `http://localhost:15005/api/contracting`.

`scripts/demo-eventos.ps1` ejecuta CU1, CU4 y CU5 seguidos.

| # | Caso de uso | Pasos | Resultado esperado |
|---|---|---|---|
| CU1 | Un plan publicado por MS2 se puede contratar | Publicar `MealPlanPublished` en `meal-plan-published`; luego `POST /v1/contracts` con ese `PlanId` y un paciente simulado | 201 `PendingPayment`; fila en `CatalogPlans` y en `InboxMessages` |
| CU2 | Mensaje duplicado | Publicar dos veces el mismo `MealPlanPublished` (mismo `Id`) con otro `Name` | Se aplica solo el primero; log `Skipping … already in the inbox` |
| CU3 | Receta de MS2 en la orden de producción | Publicar `RecipePublished` con `RecipeId` `20000000-0000-0000-0000-000000000001`; luego `GET /v1/production/daily-order?productionDate=<fecha nueva>` | Esa receta aparece con el nombre publicado; las demás siguen como `Recipe-20000000` |
| CU4 | Pago → MS4 | `POST /v1/billing/charge-plan` del contrato de CU1 | 200 `processed`; `GET /v1/billing/{factura}` muestra el nombre del plan de MS2; en ~5 s llega `ContractActivated` a `ms4.contract-activated` |
| CU5 | Paquete validado → MS5 | Orden diaria de una fecha nueva → crear paquete → `assemble` → `label` → `validate` con `totalValidatedCount: 1` | 200; llega `PackageValidated` a `ms5.package-validated` con dirección, tracking y coordenadas |
| CU6 | Broker caído | `docker stop` del RabbitMQ; hacer CU4; ver la fila en `Simulated`; `docker start` | La fila pasa sola a `Published` y el mensaje llega a la cola |
| CU7 | Plan desconocido | `POST /v1/contracts` con un `catalogPlanId` cualquiera | 404 `PLAN_NOT_FOUND` |
| CU8 | Paciente desconocido (MS1 ausente) | `POST /v1/contracts` con un `patientId` fuera de los 3 simulados | 404 `PATIENT_NOT_FOUND` |
| CU9 | Descubrimiento por gateway | Con la plataforma arriba, abrir `http://localhost:8500` y llamar a `/api/contracting/v1/sim/patients/00000000-0000-0000-0000-000000000001` en `:15005` | Servicio `contracting` en verde; 200 desde el gateway |
| CU10 | Reinicio del broker no rompe el ruteo | `docker restart` del RabbitMQ y repetir CU4 | Los bindings se reimportan y el mensaje llega (antes del arreglo se perdía) |
| CU11 | (Fase D) Catálogo real | `start-stack.ps1` con token | Los planes y recetas sembrados por MS2 aparecen solos en MS3; contratar uno de ellos |

Consultas útiles a la base de MS3:

- Plataforma: `docker exec -it nur-tricenter-postgres psql -U dev-only -d nur_tricenter_contracting`.
- MS3 solo: el contenedor `postgres` del compose standalone.

```sql
select "PlanId","Name" from "CatalogPlans";
select "RecipeId","Name" from "CatalogRecipes";
select "MessageType","ProcessedOn" from "InboxMessages" order by "ProcessedOn" desc;
select "EventType","Status","PublishedAt" from "OutgoingIntegrationEvents" order by "CreatedAt" desc;
```
