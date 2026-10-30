# Guion de presentación — Comunicación entre microservicios (MS3)

Tiempo estimado: 18–20 minutos. Todo lo de este guion se ensayó el 2026-10-30 con los servicios reales, desde `main` de cada repositorio, salvo lo que se marca como simulado.

## 0. La idea en una frase

> «Nur-Tricenter son cinco microservicios que se hablan de dos formas: **REST con JWT a través de un gateway** cuando necesito una respuesta, y **eventos por RabbitMQ** cuando solo anuncio algo que ya ocurrió. Mi servicio, MS3, hace las dos cosas, y lo diseñé para que si el otro servicio está caído, mi servicio siga funcionando.»

```
                       ┌──────────── Gateway (YARP) ◄── Consul (descubrimiento) ────────────┐
   Postman ──REST+JWT──►  /api/identity-service  /api/catalog  /api/contracting  /api/delivery-calendar  /api/nurtricenter
                       └────────────────────────────────────────────────────────────────────┘

   MS2 Catálogo ··evento··► MS3 Contratación ──REST + JWT (ms3-service)──► MS4 Calendario
                                  │                                            ▲
                                  └··package-validated··► MS5 Logística ··delivery-added-to-route / delivery-completed··┘
```
Línea continua = REST. Punteada = evento por RabbitMQ.

---

## 1. Estado de cada pieza (semáforo)

### ✅ Listo y se puede demostrar en vivo
| Pieza | Qué se ve | Cómo |
|---|---|---|
| Gateway + descubrimiento | 5 servicios «passing» en Consul y ruteados por `/api/{servicio}` | Consul UI + Postman |
| Seguridad JWT | Login, 401 sin token, 403 a una cuenta técnica con pocos permisos | Postman carpeta 0 y paso 3.5 |
| MS2 → MS3 (eventos) | El plan y las recetas del catálogo aparecen solos en MS3 | Postman 1.x, RabbitMQ UI |
| MS3 → MS4 (REST + outbox) | Pagar un contrato crea el calendario de 30 días en MS4 | Postman carpeta 2 |
| Resiliencia | Con MS4 apagado el pago responde 200 y el calendario se crea solo al volver | Terminal (paso 8) |
| MS4 → MS3 (REST) | MS3 arma la orden de producción con el menú real del día | Postman carpeta 3 |
| MS3 → MS5 (evento) | MS5 consume `PackageValidated` y agrega la entrega a la ruta del día | Postman carpeta 4 |
| MS5 → MS4 (eventos) | Al entregar en MS5, MS4 cierra el día como `Delivered` | Postman carpeta 5 |
| Contrato compartido | `PackageValidated` en el shared kernel 1.4.0 (publicado) | Repo del kernel |
| Pruebas automáticas | **197 pruebas** pasan (unitarias, integración, contrato Pact) | Test Explorer de VS 2026 |
| Documentación | Ficha de MS3 y catálogo de eventos en `knowledge` | GitHub |

### 🟡 Simulado, pero funciona (y se completa después)
| Pieza | Qué pasa hoy | Qué falta |
|---|---|---|
| **MS1 Pacientes** | MS3 conoce 3 pacientes fijos (`…0001` Juan Perez, `…0002` Juana Suarez, `…0003` Luis Ferrufino) | El MS1 real en la plataforma y un evento de paciente |
| Contratos en MS3 | MS3 usa copias locales idénticas de los contratos del kernel | Referenciar el paquete `Nur.Tricenter.Integration` 1.4.0, ya publicado |
| Base de MS5 | MS5 usa una base en Neon (la nube). Para la demo local se apunta a una base local | Un job de migración de MS5, como el de MS4 |

### 🔴 No está todavía (decirlo con honestidad)
| Pieza | Situación |
|---|---|
| **MS1 dentro de la plataforma** | `main` de `nur-tricenter-local` lo deja comentado: su repo no tiene fragmento de compose, usa SQL Server y no se registra en Consul |
| Seguridad de MS3 y MS5 | Sus endpoints son anónimos: no validan JWT todavía |
| Un paciente con dos contratos del mismo plan | Si coinciden en la misma fecha y dirección, MS3 puede asociar el paquete al día del otro contrato (ms3-gap-15) |

---

## 2. Herramientas y librerías: qué se usó para qué

| Herramienta / librería | Para qué la usamos |
|---|---|
| **.NET 8** + **FastEndpoints** | La API de MS3 (un archivo por endpoint) |
| **MediatR** | CQRS: cada caso de uso es un comando o consulta con su handler |
| **EF Core + Npgsql** | Base PostgreSQL de MS3 (`nur_tricenter_contracting`) y migraciones |
| **RabbitMQ** | Broker de eventos. Exchanges `fanout`: uno por evento; una cola por consumidor (`ms5.package-validated`) y su cola de error (`….error`) |
| **Joseco.Communication.External.RabbitMQ** | Librería que publica y consume mensajes (la misma en MS2, MS4 y MS5). Reintenta 3 veces y luego manda el mensaje a la cola de error |
| **Outbox propio** (tabla `OutgoingIntegrationEvents` + `OutboxRelay`) | Guardar el mensaje junto con el cambio y enviarlo después desde el worker |
| **Inbox** (`IdempotentConsumer` + tabla `InboxMessages`) | Ignorar mensajes repetidos |
| **Consul** | Cada servicio se registra al arrancar; el gateway lo descubre |
| **YARP** (gateway) | Proxy inverso: `/api/contracting/**` → MS3. Rutas armadas desde Consul |
| **Identity** (JWT) | Emite tokens; cada servicio valida firma, issuer, audience y scope |
| **Service account `ms3-service`** | Identidad de máquina de MS3 con mínimo privilegio para llamar a MS4 |
| **Shared kernel** (`Nur.Tricenter.Integration` 1.4.0) | Contratos de eventos compartidos entre productor y consumidor |
| **Docker Compose** | Levanta infraestructura y servicios (cada servicio aporta su fragmento `*.compose.yml`) |
| **Postman / newman** | Colección con 26 pasos y 35 comprobaciones automáticas |
| **xUnit, Moq, FluentAssertions, SQLite in-memory, WebApplicationFactory** | Pruebas unitarias e integración sin Docker |
| **PactNet** | Pruebas de contrato (consumer/provider) |
| **Repo `knowledge`** | Documentación común: fichas por servicio, gaps y catálogo de eventos |

Patrones que conviene nombrar: **API Gateway**, **Service Discovery**, **Database per Service**, **Publish/Subscribe (fanout)**, **Transactional Outbox**, **Idempotent Consumer (Inbox)**, **Dead-letter queue** (cola de error), **Consistencia eventual** (copias locales), **Service-to-service auth**.

---

## 3. Preparación (30 min antes, una sola vez)

### 3.1 Qué abrir
| Dónde | Para qué |
|---|---|
| **Docker Desktop** | Tiene que estar corriendo |
| **Terminal de VS 2026** (PowerShell), **abierta nueva** | Comandos. Nueva, para que tome `NUGET_TOKEN` y `NUGET_USERNAME` |
| **VS 2026** con `ms3-contract-and-production\Nurtricenter.MS3.sln` | Mostrar la estructura de capas y correr pruebas en **Test Explorer** |
| **Postman** | Importar la colección (3.4) |
| **Navegador**, 2 pestañas | `http://localhost:8500` (Consul) y `http://localhost:15672` (RabbitMQ, usuario `guest` / clave `guest`) |

### 3.2 Base de MS5 sin tocar Neon
MS5 guarda sus datos en Neon, una base compartida en la nube. Para no llenarla de datos de prueba, apunta MS5 a una base local **solo durante la demo**:

1. Guarda una copia de `C:\jesusferru\studies\diplomado\Nutricenter-Project\ms5-logistics-and-delivery\.env`.
2. En ese `.env`, cambia solo la línea de conexión por:
   ```
   ConnectionStrings__DefaultConnection=Host=nur-tricenter-postgres;Port=5432;Database=ms5_logistics;Username=dev-only;Password=dev-only-password
   ```
3. Instala la herramienta de migraciones una vez (si no la tienes):
   ```powershell
   dotnet tool install --global dotnet-ef
   ```
4. **Al terminar la presentación, restaura la copia del `.env`.**

### 3.3 Levantar la plataforma (≈ 3 min)
Todos los repos en `main`. En la terminal de VS 2026:

```powershell
cd C:\jesusferru\studies\diplomado\Nutricenter-Project\nur-tricenter-local
.\scripts\start-stack.ps1
```
Termina con «Stack ready». Si falla con 401 al restaurar paquetes, la terminal no tiene el token: ciérrala y abre otra.

Luego crea las tablas de MS5 en la base local (una vez por cada `start-stack`, porque la base se recrea):

```powershell
cd C:\jesusferru\studies\diplomado\Nutricenter-Project\ms5-logistics-and-delivery
dotnet ef database update --project src/Nurtricenter.Infrastructure --startup-project src/Nurtricenter.Api --connection "Host=localhost;Port=5433;Database=ms5_logistics;Username=dev-only;Password=dev-only-password"
docker restart nur-tricenter-nurtricenter-api nur-tricenter-nurtricenter-workerservice
```

Comprobar:

```powershell
docker ps --format "table {{.Names}}\t{{.Status}}"
```
Tienen que salir **14 contenedores** `(healthy)`. En `http://localhost:8500` tienen que verse **5 servicios en verde**: `catalog`, `contracting`, `delivery-calendar`, `identity-service`, `nurtricenter`.

### 3.4 Postman
1. **File → Import** → `C:\jesusferru\studies\diplomado\Nutricenter-Project\ms3-contract-and-production\postman\Nur-Tricenter-MS3.postman_collection.json`.
2. Abrir **View → Show Postman Console**: ahí la colección imprime el día elegido y el comando del paso 5.
3. Las variables `gateway` y `rabbit` ya vienen puestas (`http://localhost:15005` y `http://localhost:15672`).

### 3.5 Ensayo en seco (recomendado)
Corre las carpetas 0 a 4 con el **Runner**: **33/33** comprobaciones. Después haz la carpeta 5 a mano (ver paso 7). Es repetible: cada corrida elige un día distinto del calendario.

---

## 4. Guion paso a paso

### Paso 1 — La plataforma está viva (2 min)
**Decir:** «Tengo cinco servicios más Postgres, RabbitMQ y Consul, todos en Docker. Cada uno es dueño de su base de datos.»

**Mostrar** (terminal):
```powershell
docker ps --format "table {{.Names}}\t{{.Status}}"
```
**Observar:** cada servicio tiene **dos procesos**: la API atiende HTTP y el worker publica y consume eventos. Si cae RabbitMQ, la API no se cae.

**Mostrar** (Consul `http://localhost:8500`): los servicios en verde.
**Decir:** «Cada servicio se registra solo en Consul al arrancar, con un chequeo a `/health`. El gateway lee Consul cada 30 segundos y arma sus rutas. Agregar MS3 **no requirió tocar el código del gateway**.»

### Paso 2 — Gateway y seguridad (2 min) · Postman carpeta 0
1. **0.1 Login admin** → devuelve un `accessToken`.
   **Decir:** «Identity emite el JWT. **El gateway no lo valida**: lo reenvía, y cada servicio comprueba firma, emisor, audiencia y permisos.»
2. **0.3 MS4 SIN token** → **401**.
3. **0.2 Login ms3-service**.
   **Decir:** «Cuando MS3 llama a MS4 no usa un usuario: usa una **cuenta técnica** `ms3-service` con solo dos permisos. Mínimo privilegio.»

### Paso 3 — Eventos: MS2 → MS3 (2 min) · Postman carpeta 1
**Decir:** «MS3 no le pregunta a MS2 qué planes hay. MS2 publica un evento cuando crea un plan y MS3 guarda su propia copia.»

1. **1.1 Planes en MS2** → los planes reales del catálogo.
2. **1.2 El mismo plan visto desde MS3** → 200 con el nombre. **MS3 lo conoce sin haber llamado a MS2.**
3. Navegador **RabbitMQ → Queues**: `ms3.meal-plan-published`, `ms3.recipe-published`, `ms3.recipe-updated`, cada una con **1 consumidor**.
   **Decir:** «Un exchange por evento; cada consumidor tiene su cola `{consumidor}.{evento}`.»
4. (Opcional) Terminal:
   ```powershell
   'select "Name","TotalDays" from "CatalogPlans";' | docker exec -i nur-tricenter-postgres psql -U dev-only -d nur_tricenter_contracting
   ```

### Paso 4 — REST + outbox: pagar crea el calendario en MS4 (3 min) · Postman carpeta 2
**Decir:** «Esta es una llamada síncrona, porque MS4 expone `POST /schedule`. Pero **no la hago dentro del pago**: si MS4 está caído no quiero perder el cobro.»

1. **2.1 Crear contrato** → 201, `PendingPayment`.
2. **2.2 Pagar** → 200 `processed`, instantáneo.
3. **2.3 Fila del outbox** → pasa de `Simulated` (pendiente) a **`Published`**.
   **Decir:** «El pago guardó una fila en una tabla de salida. Un worker la toma, inicia sesión como `ms3-service`, llama a MS4 y la marca como entregada. Es el patrón **Transactional Outbox**.»
4. **2.4 Calendario creado en MS4** → `Active`, 30 días, empieza el día siguiente al pago.

### Paso 5 — Producción: MS3 lee el menú de MS4 (3 min) · Postman carpeta 3
**Decir:** «Cada noche MS4 consolida qué hay que entregar. MS3 lo consulta y lo convierte en recetas y raciones.»

1. **3.1 Ver el calendario** → la consola de Postman muestra el día elegido.
2. **3.2 Dirección del día** (la pone el paciente en MS4).
3. **3.3 Job nocturno de MS4** → 204. **Decir:** «MS4 le pide a MS2 el menú de ese día con su cuenta técnica `ms4-service`.»
4. **3.4 MS3 pide la orden a MS4 como `ms3-service`** → entregas con dirección, coordenadas y **menú**.
5. **3.5 `ms3-service` intenta editar un calendario** → **403**.
6. **3.6 Orden de producción en MS3** → recetas reales del catálogo y sus raciones.
7. **3.7 a 3.9**: crear paquete → armar → etiquetar. La etiqueta toma la dirección del día en MS4.

### Paso 6 — Evento hacia logística: MS3 → MS5 (3 min) · Postman carpeta 4
**Decir:** «Al validar la calidad, MS3 **anuncia** que el paquete está listo, sin saber quién lo escucha. MS5 lo consume y lo agrega a la ruta de ese día.»

1. **4.1 y 4.2**: en MS5 se crea un repartidor y la **ruta del día de entrega**.
   **Decir:** «MS5 necesita la ruta antes de recibir paquetes.»
2. **4.3 Validar calidad en MS3** → 200, 1 validado.
3. **4.4 Evento publicado** → `Published`, con dirección, coordenadas, fecha de entrega y `deliveryDayId` (el id del día en MS4).
4. **4.5 MS5 consumió el mensaje** → no está en la cola de error.
   Navegador **RabbitMQ → Queues**: `ms5.package-validated` vacía y con **1 consumidor**.
   **Decir:** «Si MS5 no puede procesar un mensaje, lo reintenta 3 veces y lo manda a `ms5.package-validated.error`. Así un mensaje malo no bloquea la cola y no se pierde.»
5. **4.6 Iniciar la ruta** en MS5 → 200.

### Paso 7 — Cierre del ciclo: MS5 → MS4 (2 min) · terminal + Postman carpeta 5
**Decir:** «MS5 publicó `delivery-added-to-route` y MS4 enlazó la entrega con su día. Ahora el repartidor entrega.»

1. Copia el comando que imprimió **4.4** en la consola de Postman y córrelo en la terminal. Es parecido a:
   ```powershell
   'select "DeliveryId" from "DeliveryRouteLinks" where "DayId"=64;' | docker exec -i nur-tricenter-postgres psql -U dev-only -d ms4_delivery_calendar -t
   ```
   **Decir:** «Este enlace lo guardó MS4 al consumir el evento de MS5.»
2. Pega ese id en la variable **`deliveryId`** de la colección (pestaña **Variables** → *Current value*) y guarda.
3. **5.1 Entregar** → 200 `Delivered`.
4. **5.2 Día en MS4** → `Delivered`.
   **Decir:** «Tres servicios, dos estilos de comunicación, y ninguno llamó directamente al otro para cerrar el día.»

### Paso 8 — Resiliencia: qué pasa si MS4 se cae (2 min) · terminal
```powershell
docker stop nur-tricenter-delivery-calendar-api
```
En Postman: **2.1** y **2.2** otra vez → el pago responde **200** aunque MS4 esté apagado.

```powershell
'select "EventType","Status" from "OutgoingIntegrationEvents" order by "CreatedAt" desc limit 1;' | docker exec -i nur-tricenter-postgres psql -U dev-only -d nur_tricenter_contracting
```
**Observar:** `CreateSchedule | Simulated` (pendiente).

```powershell
docker start nur-tricenter-delivery-calendar-api
```
Espera **~35 segundos** (el gateway refresca Consul cada 30 s) y repite la consulta: pasa a **`Published`** solo, y MS4 tiene **un** calendario (`POST /schedule` es idempotente por `contractId`).

> Si el tiempo apremia, cuéntalo sin ejecutarlo.

### Paso 9 — Shared kernel, calidad y documentación (2 min)
1. Repo `nur-tricenter-shared-kernels`: `nur-tricenter-integration\Messages\Contracting\PackageValidated.cs`.
   **Decir:** «Los contratos viven en un paquete compartido. `PackageValidated` lo agregué yo; después el equipo le sumó la fecha de entrega, el nombre del paciente y el id del día de MS4. Va en la versión 1.4.0.»
2. **VS 2026 → Test Explorer → Run All Tests**: **197 pruebas** (143 unitarias, 51 de integración, 3 de contrato Pact). No necesitan Docker.
   Por terminal: `cd C:\jesusferru\studies\diplomado\Nutricenter-Project\ms3-contract-and-production` y `dotnet test .\Nurtricenter.MS3.sln`.
3. Repo `knowledge`: `services/ms3-contracting/` y `contracts/events.yaml`.
   **Decir:** «Documenté también lo que no está resuelto, en la sección de gaps.»

---

## 5. Cierre (1 min)
> «Resumen: MS3 consume eventos de MS2, llama por REST seguro a MS4 y publica un evento que MS5 consume; MS5 a su vez avisa a MS4 para cerrar el día. Todo pasa por el gateway, con descubrimiento en Consul. Usé outbox e inbox para que ninguna caída pierda ni duplique un mensaje. Queda por completar el MS1 real y la seguridad en MS3.»

---

## 6. Si algo falla (plan B)

| Síntoma | Qué hacer |
|---|---|
| `start-stack` falla con 401 | Cerrar la terminal y abrir otra (toma las variables del token) |
| `start-stack` no conecta con Docker | Abrir Docker Desktop y esperar a que diga «Engine running» |
| Postman 404 en `/api/...` los primeros 30 s | El gateway aún no ve el servicio: esperar medio minuto |
| 2.3 se queda en `Simulated` | Esperar y repetir; si sigue, `docker logs nur-tricenter-contracting-workerservice --tail 30` |
| MS5 da 500 o el worker de MS5 muestra `relation … does not exist` | Faltan las tablas de MS5: correr `dotnet ef database update` (3.3) y reiniciar sus contenedores |
| 4.5 falla: el paquete está en `ms5.package-validated.error` | MS5 no tenía ruta para esa fecha. Revisar 4.2 y repetir 3.7 a 4.5 con un paquete nuevo |
| 4.4 sin dirección (`Address not found`) | 3.2 o 3.3 no se ejecutaron para ese día. Repetir la carpeta 3 |
| 5.1 da 400 «Route Id must not be empty» | Falta correr la carpeta 4 en la misma sesión de Postman, o no se guardó `deliveryId` |
| Se rompió todo | Mostrar la colección corrida de antemano y el código; o `docker compose … down -v` y repetir 3.3 |

Para bajar todo al terminar:
```powershell
cd C:\jesusferru\studies\diplomado\Nutricenter-Project\nur-tricenter-local
docker compose -f compose/docker-compose.yml -f compose/docker-compose.infra.yml -p nur-tricenter down -v --remove-orphans
```
Y **restaura el `.env` de MS5** (3.2).

---

## 7. Preguntas probables del profesor

**¿Por qué REST con MS4 y eventos con MS5?**
MS4 expone un `POST /schedule` pensado para esto y yo necesito saber si se aceptó o se rechazó. En cambio «el paquete está listo» es un hecho que anuncio sin necesitar respuesta, y puede interesar a más de un servicio.

**¿Qué pasa si el mismo mensaje llega dos veces?**
RabbitMQ entrega «al menos una vez». Cada mensaje lleva un `Id` y el consumidor lo guarda en `InboxMessages`: el segundo se ignora. Y `POST /schedule` es idempotente por `contractId`.

**¿Y si MS5 no puede procesar el mensaje?**
La librería lo reintenta 3 veces y lo manda a la cola de error (`ms5.package-validated.error`). No se pierde y no bloquea a los siguientes. Hoy pasa, por ejemplo, si no existe una ruta para la fecha de entrega.

**¿Y si RabbitMQ o MS4 se caen justo después de cobrar?**
El cobro y la fila del outbox quedan guardados; el worker reintenta cada 5 segundos hasta que puede entregar. Se vio en el paso 8.

**¿Por qué el gateway no valida el token?**
Decisión del equipo: el gateway solo enruta. Cada servicio valida el JWT y el permiso del endpoint.

**¿Para qué sirve Consul?**
Para no fijar direcciones. Cada servicio se registra y el gateway descubre cuáles están sanos. Si un servicio falla su chequeo de salud, deja de recibir tráfico.

**¿Por qué una cuenta técnica y no un usuario?**
Las llamadas entre servicios no tienen un usuario detrás. `ms3-service` tiene solo los permisos que necesita y se puede revocar sin afectar a nadie más. Intentar cancelar un calendario da 403.

**¿Por qué MS3 guarda una copia de los planes si ya están en MS2?**
Para no depender de MS2 en cada contrato y para que la base de cada servicio sea suya. El costo es la consistencia eventual: la copia se actualiza con un pequeño retraso.

**¿Por qué los contratos de eventos van en un paquete compartido?**
Para que productor y consumidor compilen contra el mismo tipo. Si se copian, un cambio de un campo se pierde en silencio en el otro lado.

**¿Qué haría falta para ponerlo en producción?**
Autenticación en MS3 y MS5, el MS1 real, que MS3 referencie el kernel en lugar de copias, mensajes persistentes en RabbitMQ (la librería actual los publica no persistentes) y que el outbox guarde el cambio y su fila en una sola transacción (ms3-gap-11).

**¿Qué encontraste y corregiste en el camino?**
- RabbitMQ perdía en silencio los mensajes tras reiniciar: la plataforma ahora recarga la topología en cada arranque.
- La orden de producción daba 500 con planes reales: ahora MS4 manda el menú y, si falta, MS3 responde 422.
- La orden diaria no se guardaba en la base, y la etiqueta buscaba siempre la dirección de «mañana». Al probar la cadena completa con MS5, los paquetes de otros días salían sin dirección y MS5 los rechazaba. Ya está corregido.

---

## 8. Referencia rápida

| Dato | Valor |
|---|---|
| Gateway | `http://localhost:15005` |
| Consul | `http://localhost:8500` |
| RabbitMQ | `http://localhost:15672` · `guest` / `guest` |
| Admin | `admin` / `admin` |
| Cuenta técnica de MS3 | `ms3-service` |
| Rutas en el gateway | `/api/contracting`, `/api/delivery-calendar`, `/api/catalog`, `/api/identity-service`, `/api/nurtricenter` |
| Base de MS3 | `nur_tricenter_contracting` |
| Tablas clave de MS3 | `CatalogPlans`, `CatalogRecipes` (copias de MS2) · `InboxMessages` (inbox) · `OutgoingIntegrationEvents` (outbox) |
| Estados del outbox | `Simulated` = pendiente · `Published` = entregado · `Rejected` = MS4 lo rechazó · `Unroutable` = sin destino |
| Colas de MS5 | `ms5.package-validated` y su cola de error `ms5.package-validated.error` |
| Enlace entrega ↔ día | Tabla `DeliveryRouteLinks` en `ms4_delivery_calendar` |
| Planes con 15 o 30 días | MS4 solo acepta esas dos duraciones |
