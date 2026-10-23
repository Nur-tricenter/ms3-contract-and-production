---
name: pact-testing
description: Entorno validado de contract testing (Pact) del microservicio Nurtricenter MS3. Úsalo siempre antes de escribir, modificar o depurar una prueba de contrato, o cuando haya que explicar cómo se hostea el provider, qué convenciones sigue el consumer, o por qué una interacción no matchea.
---

# Entorno de contract testing (Pact) — Nurtricenter MS3

Este entorno ya está construido y verificado: **PactNet 5.0.1**, un consumer que
genera un pacto real (`.json`) y un provider que lo verifica contra la API real
de Nurtricenter en memoria. No lo rediseñes sin necesidad — las restricciones de
abajo costaron sesiones de depuración real, no son hipotéticas.

## Ubicación

| Proyecto | Ruta | Rol |
|---|---|---|
| Consumer | `Nurtricenter/Nurtricenter.MS3.PactTests.Consumer/` | Define interacciones, genera `pacts/*.json` |
| Provider | `Nurtricenter/Nurtricenter.MS3.PactTests.Provider/` | Hostea la API real en un puerto TCP real y verifica el pacto |
| Pacto generado | `Nurtricenter.MS3.PactTests.Consumer/pacts/Nurtricenter Contracts Client-Nurtricenter MS3.json` | Se commitea al repo — es el contrato, no un artefacto de build |

## Comandos

```bash
# 1. Generar/regenerar el pacto (consumer)
dotnet test Nurtricenter/Nurtricenter.MS3.PactTests.Consumer/Nurtricenter.MS3.PactTests.Consumer.csproj

# 2. Verificar el pacto contra la API real (provider) — requiere que el paso 1 haya corrido antes
dotnet test Nurtricenter/Nurtricenter.MS3.PactTests.Provider/Nurtricenter.MS3.PactTests.Provider.csproj

# Suite completa (unitarias + integración + contrato)
dotnet test Nurtricenter/Nurtricenter.MS3.sln
```

Todos se ejecutan desde la raíz del repositorio. No requieren Docker ni
PostgreSQL — el provider usa SQLite in-memory, igual que
`Nurtricenter.MS3.IntegrationTests`.

## Restricciones conocidas

Estas ya costaron tiempo real de depuración. Respétalas.

### Nada de acentos en `UponReceiving` ni en `Given`

Con PactNet 5.0.1 sobre este entorno, una descripción de interacción o un
nombre de estado con una tilde (`á`, `é`, `í`, `ó`, `ú`, `ñ`) corrompe el
registro de la interacción en el backend nativo de forma silenciosa: no lanza
excepción, pero el mock server responde **500 "Request was not expected"** a
**cualquier** request real, sin importar si el método/ruta/body coinciden. Se
reprodujo de forma determinista aislando variable por variable: la misma
interacción con la tilde quitada pasa sin cambiar nada más.

Escribe las descripciones y los nombres de estado en español sin tildes
(`"una solicitud para crear un contrato con paciente y plan validos"`, no
`"válidos"`). Si hace falta una palabra que solo se distingue por la tilde,
usa el equivalente sin acentuar; no es incorrecto para este propósito, es una
limitación de la herramienta, no del idioma.

### El provider no puede correr sobre `WebApplicationFactory`

PactNet verifica con su backend nativo en Rust, que necesita un socket TCP
real. `WebApplicationFactory`/`Microsoft.AspNetCore.Mvc.Testing` exponen un
`TestServer` en memoria que el proceso Rust no puede alcanzar — todas las
verificaciones fallarían.

`PactProviderHost` (en `Nurtricenter.MS3.PactTests.Provider/PactProviderHost.cs`)
resuelve esto con `WebApplication.CreateBuilder()` normal, enlazado a un puerto
TCP real (`http://127.0.0.1:9393`), reconstruyendo el mismo pipeline que
`Program.cs` (`AddFastEndpoints`, `AddApplication`, `AddInfrastructure`) y
sustituyendo PostgreSQL por SQLite in-memory con la misma técnica
`RemoveDbContextRegistrations` que ya usa `ApiFactory` en el skill
`integration-testing`. No lo cambies a `WebApplicationFactory`; no va a
funcionar aunque compile.

### FastEndpoints no descubre los endpoints solo

Como el provider hostea la API desde el ensamblado de
`Nurtricenter.MS3.PactTests.Provider` (no desde `Nurtricenter.Api`), el
autodescubrimiento de `AddFastEndpoints()` no encuentra los endpoints reales y
falla con `FastEndpoints was unable to find any endpoint declarations!`. Por
eso `PactProviderHost` llama:

```csharp
builder.Services.AddFastEndpoints(o => o.Assemblies = new[] { typeof(CreateContractEndpoint).Assembly });
```

Si agregas una interacción contra un endpoint de otro ensamblado, ese
ensamblado ya está cubierto (todos los endpoints viven en `Nurtricenter.Api`),
no hace falta tocar esta línea.

### El cliente del consumer tiene que mandar de verdad los headers que declaras

Si `WithHeader("Accept", "application/json")` está en la interacción pero el
código del cliente nunca setea ese header, el mock server rechaza el request
real (headers declarados son una promesa sobre lo que el cliente manda de
verdad, no decoración). `ContractsApiClient` setea `Accept: application/json`
en el constructor por esta razón — si agregas un header a una interacción,
agrégalo también al cliente real.

### `Match.Type(...)` no fue la causa del bug original

Durante el desarrollo se sospechó que los matchers (`Match.Type(Guid.NewGuid())`,
`Match.Type(DateTime.UtcNow)`) rompían el matching. Se descartó con un
diagnóstico aislado: el problema real eran las tildes. Los matchers funcionan
como documenta el README de `pact-net` — úsalos igual que en
`ContractsApiConsumerTests.cs` para campos que decide el provider (`id`,
`createdAt`), y valores literales para los que decide el consumer o que son
una garantía real del contrato (`status: "PendingPayment"` al crear).

## Qué se mockea y qué no

Igual que en `unit-testing` e `integration-testing`: el **provider no mockea
nada** — corre el código real (handlers, agregados, EF Core) contra SQLite. El
**consumer no habla con la API real en ningún momento** — solo con el mock
server que levanta PactNet a partir de las interacciones declaradas.

## Provider states usados

| Estado | Qué hace el handler en `PactProviderHost` |
|---|---|
| `el paciente y el plan existen` | Nada — los datos de simulación son estáticos y siempre existen |
| `existe un contrato` (param `contractId`) | Crea un `Contract` real vía `Contract.Create(...)` y fuerza su `Id` al GUID que pide la interacción con `db.Entry(contract).Property("Id").CurrentValue = contractId` (el factory del agregado no expone forma pública de fijar el Id) |

## Si algo falla

| Síntoma | Causa probable |
|---|---|
| Todo interacción da 500 "Request was not expected" sin importar el request | Una tilde en `UponReceiving`/`Given` — ver arriba |
| `FastEndpoints was unable to find any endpoint declarations!` | Falta `o.Assemblies = new[] { typeof(...).Assembly }` en `AddFastEndpoints` |
| El provider no arranca / puerto en uso | Otro proceso ya tiene el `9393`; cambiá `PactProviderHost.ServerUri` si hace falta |
| `WithFileSource` no encuentra el archivo | Corré primero el consumer (paso 1) para generar `pacts/*.json`; revisá que el nombre coincida con `"{Consumer}-{Provider}.json"` |
| El provider verifica pero el estado no quedó sembrado | El nombre del estado en el provider-state handler no coincide **exactamente** (case-sensitive, sin tildes) con el `Given(...)` del consumer |
