---
name: unit-testing
description: Entorno validado de pruebas unitarias del microservicio Nurtricenter MS3. Úsalo siempre antes de escribir, modificar o depurar una prueba unitaria de este repositorio, o cuando haya que explicar qué se mockea, cómo se mide el code coverage, o qué comando corre la suite.
---

# Entorno de pruebas unitarias — Nurtricenter MS3

Este entorno ya está construido y verificado: pruebas unitarias pasando sobre
Domain y Application. No lo rediseñes.

## Ubicación

| Proyecto / carpeta | Ruta | Rol |
|---|---|---|
| Pruebas unitarias | `Nurtricenter/Nurtricenter.MS3.Tests/` | Aquí van las pruebas nuevas |
| `Domain/` | `Nurtricenter.MS3.Tests/Domain/` | Agregados, entidades y value objects de `Nurtricenter.MS3.Core`. Sin mocks. |
| `Application/` | `Nurtricenter.MS3.Tests/Application/` | `IRequestHandler` de MediatR en `Nurtricenter.MS3.Application`. Con mocks. |
| Pruebas de integración | `Nurtricenter/Nurtricenter.MS3.IntegrationTests/` | No se toca desde este skill — usa el skill `integration-testing` |
| Solución | `Nurtricenter/Nurtricenter.MS3.sln` | Agrupa los cinco proyectos |

## Comandos

```bash
# Solo las pruebas unitarias
dotnet test Nurtricenter/Nurtricenter.MS3.Tests/Nurtricenter.MS3.Tests.csproj

# Con reporte de code coverage (coverage.cobertura.xml)
dotnet test Nurtricenter/Nurtricenter.MS3.Tests/Nurtricenter.MS3.Tests.csproj --collect:"XPlat Code Coverage" --results-directory Nurtricenter/Nurtricenter.MS3.Tests/TestResults

# Suite completa (unitarias + integración)
dotnet test Nurtricenter/Nurtricenter.MS3.sln
```

Todos se ejecutan desde la raíz del repositorio.

El coverage se reparte por paquete dentro de `coverage.cobertura.xml`. Lee el
`line-rate` de los paquetes `Nurtricenter.MS3.Core` y
`Nurtricenter.MS3.Application` — son los que este proyecto prueba. Los paquetes
`Nurtricenter.MS3.Api` e `Infrastructure` quedan en 0% a propósito: ese código
se prueba con HTTP real en `Nurtricenter.MS3.IntegrationTests`, no aquí.

## Frameworks

xUnit + Moq + FluentAssertions + `coverlet.collector`. Ya están referenciados en
`Nurtricenter.MS3.Tests.csproj`; no agregues paquetes nuevos sin necesidad real.

## Qué se mockea y qué no

| Capa | Se mockea | No se mockea |
|---|---|---|
| `Domain/` (Core) | Nada. Los agregados y value objects se prueban en aislamiento puro, invocando sus métodos directamente. | El propio agregado bajo prueba. |
| `Application/` (Handlers) | Las dependencias externas del handler: repositorios (`IContractRepository`, `IControlChargeRepository`, `IDailyProductionOrderRepository`, `IPackageRepository`, `IOutgoingEventRepository`), `IUnitOfWork` y `ISimulationService`. | El handler bajo prueba, y las entidades de dominio que construye — si necesitas un `Contract` en cierto estado, créalo con sus métodos reales (`Contract.Create(...)`, `.ProcessPayment(...)`), no lo mockees. |

Patrón habitual para un handler (ver `Nurtricenter.MS3.Tests/Application/*.cs`
para ejemplos reales):

```csharp
private readonly Mock<IContractRepository> _repository = new();
private readonly Mock<IUnitOfWork> _unitOfWork = new();
private readonly Mock<ISimulationService> _simulation = new();
```

Verifica el efecto, no solo el resultado:

```csharp
_repository.Verify(r => r.AddAsync(It.IsAny<Contract>()), Times.Once);
_unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
```

## Reglas

1. **Una responsabilidad por prueba.** Si el nombre necesita "y" para
   describirla, probablemente son dos pruebas.
2. **AAA sin comentarios que lo anuncien.** Arrange, Act, Assert como bloques
   separados por una línea en blanco; el código ya lo deja claro.
3. **Nombres descriptivos**, patrón `<Método>_<condición>_<resultado
   esperado>`. La suite existente quedó en inglés (p. ej.
   `ProcessPayment_rejects_payment_when_contract_is_not_pending_payment`);
   para pruebas nuevas usa español, igual que las de integración
   (`<VERBO>_<recurso>_<resultado esperado>`), salvo que te pidan mantener
   consistencia con el archivo que estás extendiendo.
4. **`[Theory]` con `[InlineData]`** cuando 3+ `[Fact]` solo cambian el valor de
   entrada. No lo fuerces si los casos tienen asserts distintos.
5. **No dependas de PostgreSQL, HTTP ni `WebApplicationFactory`.** Si una prueba
   necesita eso, es una prueba de integración — va en el otro proyecto.
6. **Los handlers dependen de `ISimulationService` (la interfaz), no de su
   implementación concreta.** No instancies `SimulationDataService` real en una
   prueba de `Application/`.

## Si algo falla

| Síntoma | Causa probable |
|---|---|
| El mock no intercepta la llamada | Falta `.Setup(...)` para ese método antes de invocar el handler, o el argumento no matchea (`It.IsAny<T>()` vs. un valor concreto) |
| `Moq.MockException: ... invocation was not performed` | El handler nunca llamó al método verificado, o se verificó sobre el mock equivocado |
| El coverage del paquete `Api`/`Infrastructure` da 0% | Es esperado — ese código se prueba en `Nurtricenter.MS3.IntegrationTests` |
| `dotnet test` no encuentra `coverlet.collector` | Corre `dotnet restore` primero; el paquete ya está en el `.csproj` |
