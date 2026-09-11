---
name: integration-testing
description: Entorno validado de pruebas de integración del microservicio Nurtricenter MS3. Úsalo siempre antes de escribir, modificar o depurar una prueba de integración de este repositorio, o cuando haya que explicar cómo se levanta la API en memoria, por qué se sustituye PostgreSQL por SQLite, o qué comando corre la suite.
---

# Entorno de pruebas de integración — Nurtricenter MS3

Este entorno ya está construido y verificado. No lo rediseñes: la API arranca en
memoria con `WebApplicationFactory` y PostgreSQL se sustituye por SQLite
in-memory. Antes de tocar `ApiFactory`, lee la sección "Restricciones conocidas".

## Ubicación

| Proyecto | Ruta | Rol |
|---|---|---|
| API | `Nurtricenter/Nurtricenter.Api/` | Arranca en memoria; expone `Program` |
| Tests de integración | `Nurtricenter/Nurtricenter.MS3.IntegrationTests/` | Aquí van las pruebas nuevas |
| Tests unitarios | `Nurtricenter/Nurtricenter.MS3.Tests/` | No se toca desde este skill |
| Solución | `Nurtricenter/Nurtricenter.MS3.sln` | Agrupa los cinco proyectos |

## Comandos

```bash
# Solo las pruebas de integración
dotnet test Nurtricenter/Nurtricenter.MS3.IntegrationTests/Nurtricenter.MS3.IntegrationTests.csproj

# Suite completa (unitarias + integración)
dotnet test Nurtricenter/Nurtricenter.MS3.sln
```

Ambos se ejecutan desde la raíz del repositorio.

## API de `ApiFactory`

Vive en `Nurtricenter.MS3.IntegrationTests/ApiFactory.cs`. Hereda de
`WebApplicationFactory<Program>`.

| Miembro | Para qué sirve |
|---|---|
| `CreateApiClient()` | Devuelve un `HttpClient` que habla con la API real. Crea el esquema la primera vez. |
| `SeedAsync(db => ...)` | Siembra datos de apoyo antes de ejercitar un flujo. |
| `QueryDbAsync(db => ...)` | Consulta la base para verificar que el estado quedó persistido. |

Las pruebas se enganchan con `IClassFixture<ApiFactory>`. **No** se usa
`ICollectionFixture`: cada clase de prueba debe recibir su propia instancia para
que la base arranque limpia y las pruebas no se contaminen entre sí.

## Reglas

1. **Nada de mocks.** Es una prueba de integración: se ejercita el endpoint HTTP
   contra la base de datos real. Los dobles de prueba ya viven en
   `Nurtricenter.MS3.Tests/`.
2. **No se modifica código de producción** para hacer pasar una prueba. Si un
   endpoint falla, el hallazgo se reporta; no se ajusta el endpoint.
3. **Se verifica el estado persistido**, no solo el código HTTP. Usa
   `QueryDbAsync` para confirmar que el efecto sobrevivió al request.
4. **Las pruebas deben poder correr en paralelo y en cualquier orden.** Cada
   clase crea datos propios; nunca dependas de lo que sembró otra clase.
5. **Nombres en español y descriptivos**, con el patrón
   `<VERBO>_<recurso>_<resultado esperado>`. Ejemplo:
   `POST_contracts_devuelve_404_cuando_el_paciente_no_existe`.

## Restricciones conocidas

Estas tres ya costaron tiempo de depuración. Respétalas.

### La ruta de base de datos no viene de `appsettings.json`

`Program.cs` sólo migra y siembra cuando `IsDevelopment()` es verdadero, y
`WebApplicationFactory` arranca en `Development` por defecto. Si no se
intercepta, la suite intentaría migrar contra el PostgreSQL de `localhost:5432`
y fallaría. Por eso `ApiFactory` fuerza `builder.UseEnvironment("Testing")`.
No quites esa línea.

### `RemoveDbContextRegistrations` no es opcional

Quitar sólo `DbContextOptions<ApplicationDbContext>` no basta y produce el error
*"Only a single database provider can be registered for the 'ApplicationDbContext'
service"*.

La causa: `AddDbContext` guarda su acción de configuración en un descriptor
`IDbContextOptionsConfiguration<TContext>` y lo registra con `TryAdd`. La
segunda llamada a `AddDbContext` no reemplaza nada, así que Npgsql sigue
ganando. Ese tipo es **interno** en EF Core 8 — de ahí que
`RemoveDbContextRegistrations` lo localice por nombre en lugar de por tipo.

Si en el futuro se agrega otro `DbContext`, hay que extender ese método.

### El esquema se crea con `EnsureCreated`, no con migraciones

SQLite in-memory no ejecuta las migraciones de Npgsql (usan tipos como `jsonb`).
`ApiFactory.EnsureSchema()` llama a `EnsureCreated()`, que construye el esquema
desde el modelo de EF Core. Consecuencia: **un cambio en las configuraciones de
entidad se refleja solo**, pero las migraciones de
`Nurtricenter.MS3.Infrastructure/Data/Migrations/` pueden quedar desalineadas
del esquema de pruebas. Es aceptable para pruebas; no lo es para producción.

## Si algo falla

| Síntoma | Causa probable |
|---|---|
| `Only a single database provider...` | Se rompió `RemoveDbContextRegistrations` |
| Intenta conectar a `localhost:5432` | Falta `UseEnvironment("Testing")` |
| El esquema no existe / no such table | No se llamó a `CreateApiClient()` antes de consultar |
| `WebApplicationFactory<Program>` no compila | Falta `public partial class Program { }` al final de `Program.cs` |
| Los datos de una clase aparecen en otra | Se está compartiendo la factory entre clases |
