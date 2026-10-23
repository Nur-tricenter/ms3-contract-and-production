# Proyecto Final — Capa de Testing completa (Nurtricenter MS3)

Certifica la actividad final del taller de testing, apoyada en lo construido en
[Tarea 1](TAREA1-Testing.md) (unit tests), [Tarea 2](TAREA2-Testing.md)
(integration tests) y [Tarea 3](TAREA3-Testing.md) (contract testing con
Pact), sobre el caso de estudio del proyecto final: el microservicio
**Nurtricenter MS3**.

**Repositorio:** https://github.com/JesusFerru/dipl-arqui-microservicios
**Rama:** `feat/proyecto-final-testing`

**Aclaración sobre "cada uno de los microservicios":** MS1, MS2, MS4 y MS5 no
tienen código propio en este repositorio — están simulados de forma
determinista dentro de `Nurtricenter.MS3.Application/Simulations/` (ver
`CLAUDE.md`). El único microservicio real del caso de estudio es **MS3**, así
que la capa de testing completa se implementó sobre él.

## Qué pedía la actividad

1. Capa de testing completa: unit tests + integration tests.
2. Unit tests: reporte de coverage con **mínimo 80%**.
3. Integration tests: al menos **dos flujos completos**, agrupados por flujo.
4. Si se usó código generado, incluir las skills y reglas del proyecto.
5. Presentar código de los tests, el reporte de coverage, el código testeado, y
   un **video de presentación individual**.
6. Enlace al repositorio.

## 1. Unit tests — coverage ≥ 80% (cumplido)

[`Nurtricenter.MS3.Tests`](Nurtricenter/Nurtricenter.MS3.Tests/) — **121
pruebas**, todas en verde:

```bash
dotnet test Nurtricenter/Nurtricenter.MS3.Tests/Nurtricenter.MS3.Tests.csproj
# Passed! - Failed: 0, Passed: 121, Skipped: 0, Total: 121
```

### Reporte de coverage

Generado con `coverlet.collector` + `reportgenerator`, commiteado en
[`Nurtricenter/coverage-report/`](Nurtricenter/coverage-report/index.html)
(abrí `index.html` para navegarlo por clase).

```bash
cd Nurtricenter
dotnet test Nurtricenter.MS3.Tests/Nurtricenter.MS3.Tests.csproj \
  --collect:"XPlat Code Coverage" --results-directory Nurtricenter.MS3.Tests/TestResults

reportgenerator \
  -reports:"Nurtricenter.MS3.Tests/TestResults/*/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:"Html;TextSummary;MarkdownSummaryGithub"
```

| Paquete | Line coverage | Branch coverage |
|---|---|---|
| **Nurtricenter.MS3.Application** | **91.4%** | 85.4% |
| **Nurtricenter.MS3.Core** (Domain) | **89.0%** | 83.3% |
| **Total del reporte** (los 3 ensamblados que mide `Nurtricenter.MS3.Tests`, incluyendo `Api`) | **87.5%** (691/789 líneas) | 82.8% |

Los tres números superan el 80% exigido — incluso el total crudo del reporte,
que castiga con 0% a `Nurtricenter.MS3.Api` (el `Program.cs` de arranque, sin
lógica propia: FastEndpoints, Swagger y DI). Ese 0% es intencional, no un
descuido: `Program.cs` y toda la capa de infraestructura (`Nurtricenter.MS3.Infrastructure`,
EF Core, repositorios) se validan con HTTP real contra una base real en
`Nurtricenter.MS3.IntegrationTests`, que es la herramienta correcta para
probar arranque y persistencia — no un unit test.

### Qué se agregó para esta actividad

Sobre las 88 pruebas de la Tarea 1 (handlers con mocks de Moq + agregados de
dominio sin mocks), se sumaron 33 pruebas nuevas para cerrar los huecos reales
de cobertura en `Application`:

- [`SimulationDataServiceTests.cs`](Nurtricenter/Nurtricenter.MS3.Tests/Application/SimulationDataServiceTests.cs) —
  la implementación real de `ISimulationService` nunca se ejercitaba
  directamente (los handlers la mockean vía interfaz). Cubre los cuatro
  métodos públicos contra los identificadores deterministas del skill
  `test-flows`, y de paso documenta a nivel unitario el mismo hallazgo de la
  Tarea 2: `GetActiveCalendars` no tiene ninguna entrada para Luis Ferrufino.
- [`DtosTests.cs`](Nurtricenter/Nurtricenter.MS3.Tests/Application/DtosTests.cs) —
  los DTOs (records planos de request/response) no tenían ninguna prueba
  directa. Son pruebas deliberadamente simples (construir + verificar
  propiedades) porque no hay lógica que probar; su único propósito es cerrar
  el número, no simular profundidad que no existe.

Usa el mismo entorno validado de la Tarea 1: skill
[`unit-testing`](.claude/skills/unit-testing/SKILL.md) y agente
[`test-writer`](.claude/agents/test-writer.md) — no se creó nada nuevo para
esta actividad, se reusó lo que ya existía.

## 2. Integration tests — dos flujos completos, agrupados (cumplido)

Ya construido en la Tarea 2, sin cambios para esta actividad. Las pruebas ya
están agrupadas por flujo, un archivo por flujo:

| Archivo | Flujo |
|---|---|
| [`ContractLifecycleTests.cs`](Nurtricenter/Nurtricenter.MS3.IntegrationTests/ContractLifecycleTests.cs) | Flujo 1 — ciclo de vida completo del contrato (contratar → pagar → cancelar) |
| [`PackageLifecycleTests.cs`](Nurtricenter/Nurtricenter.MS3.IntegrationTests/PackageLifecycleTests.cs) | Flujo 2 — ciclo de vida completo del paquete (crear → ensamblar → etiquetar → validar) |
| [`ContractErrorFlowTests.cs`](Nurtricenter/Nurtricenter.MS3.IntegrationTests/ContractErrorFlowTests.cs) | Flujo de error — 12 escenarios agrupados por los códigos que el sistema realmente devuelve |
| [`HealthCheckTests.cs`](Nurtricenter/Nurtricenter.MS3.IntegrationTests/HealthCheckTests.cs) | Smoke tests de arranque |

```bash
dotnet test Nurtricenter/Nurtricenter.MS3.IntegrationTests/Nurtricenter.MS3.IntegrationTests.csproj
# Passed! - Failed: 0, Passed: 18, Skipped: 0, Total: 18
```

El enunciado permite Postman, el lenguaje del proyecto, o Pact — este proyecto
usa **los tres**: integración en C# (arriba) más contract testing en
[Tarea 3](TAREA3-Testing.md) con PactNet (2 solicitudes del consumer,
verificadas de verdad contra la API real corriendo en un puerto TCP real):

```bash
dotnet test Nurtricenter/Nurtricenter.MS3.PactTests.Consumer/Nurtricenter.MS3.PactTests.Consumer.csproj
dotnet test Nurtricenter/Nurtricenter.MS3.PactTests.Provider/Nurtricenter.MS3.PactTests.Provider.csproj
```

## 3. Skills y reglas del proyecto usadas para el código generado

Las cuatro capas de testing tienen su entorno documentado y su par de
subagentes escritor/verificador, todo declarado en [`CLAUDE.md`](CLAUDE.md):

| Capa | Skill | Escritor | Verificador |
|---|---|---|---|
| Unit tests | [`unit-testing`](.claude/skills/unit-testing/SKILL.md) | [`test-writer`](.claude/agents/test-writer.md) | [`test-verifier`](.claude/agents/test-verifier.md) |
| Integration tests | [`integration-testing`](.claude/skills/integration-testing/SKILL.md) + [`test-flows`](.claude/skills/test-flows/SKILL.md) | [`integration-test-writer`](.claude/agents/integration-test-writer.md) | [`integration-test-verifier`](.claude/agents/integration-test-verifier.md) |
| Contract tests | [`pact-testing`](.claude/skills/pact-testing/SKILL.md) | [`pact-writer`](.claude/agents/pact-writer.md) | [`pact-checker`](.claude/agents/pact-checker.md) |

## 4. Código testeado

El código bajo prueba es el microservicio completo en
[`Nurtricenter/`](Nurtricenter/) — `Nurtricenter.MS3.Core` (dominio),
`Nurtricenter.MS3.Application` (casos de uso), `Nurtricenter.MS3.Infrastructure`
(persistencia) y `Nurtricenter.Api` (endpoints), tal como lo documenta el
[`README.md`](Nurtricenter/README.md) del microservicio.

## 5. Video de presentación individual

**Pendiente — es responsabilidad del presentador, no se puede generar por
este medio.** Según el enunciado no hace falta que sea largo: alcanza con
explicar de forma clara y concisa qué se está entregando (esta capa de
testing) y cómo se ejecuta (los comandos de este documento). Guion sugerido de
3-5 minutos:

1. Estructura del proyecto y las 4 capas de testing (30 seg).
2. Unit tests: correr `dotnet test Nurtricenter.MS3.Tests` y mostrar el
   reporte de coverage abierto en el navegador (`coverage-report/index.html`),
   señalando que Application y Core superan el 80% (1 min).
3. Integration tests: correr `Nurtricenter.MS3.IntegrationTests` y mostrar
   brevemente `ContractLifecycleTests.cs` o `PackageLifecycleTests.cs` como
   ejemplo de flujo completo (1 min).
4. Contract testing: correr el consumer y el provider de Pact, mostrar el
   `.json` generado (1 min).
5. Mención rápida del entorno de IA (skills + agentes escritor/verificador) y
   cierre (30 seg).

## 6. Cómo reproducir todo

```bash
git clone https://github.com/JesusFerru/dipl-arqui-microservicios.git
cd dipl-arqui-microservicios
git checkout feat/proyecto-final-testing

dotnet build Nurtricenter/Nurtricenter.MS3.sln
dotnet test  Nurtricenter/Nurtricenter.MS3.sln
```

No requiere Docker ni PostgreSQL para ninguna capa de testing.

## Estado combinado de toda la suite

```bash
dotnet test Nurtricenter/Nurtricenter.MS3.sln
```

```
Nurtricenter.MS3.Tests.dll:                121 passed, 0 failed   (unit — 91.4% Application, 89.0% Core)
Nurtricenter.MS3.IntegrationTests.dll:      18 passed, 0 failed   (integration — 2 flujos completos + errores)
Nurtricenter.MS3.PactTests.Consumer.dll:     2 passed, 0 failed   (contract — consumer)
Nurtricenter.MS3.PactTests.Provider.dll:     1 passed, 0 failed   (contract — provider)
Total: 142 passed, 0 failed
```

## Extensión opcional: Postman

No se construyó una colección de Postman porque el enunciado permite elegir
entre Postman, el lenguaje del proyecto o Pact, y ya se cubrieron las otras
dos opciones — agregar Postman encima sería redundante para cumplir el
requisito. Si más adelante se quiere sumar de todas formas (por ejemplo para
una demo más visual), el punto de partida natural es
[`.claude/skills/test-flows/SKILL.md`](.claude/skills/test-flows/SKILL.md):
ya documenta cada ruta, payload, precondición y código esperado que
`ContractErrorFlowTests.cs`, `ContractLifecycleTests.cs` y
`PackageLifecycleTests.cs` ejercitan — es la fuente de verdad para armar una
colección sin tener que releer el código. Iría en una carpeta nueva
`Nurtricenter/postman/`, con una colección por flujo, igual que ya están
agrupados los integration tests.

## Notas honestas

- El 80% de coverage se mide sobre `Nurtricenter.MS3.Application` y
  `Nurtricenter.MS3.Core` — las capas que este proyecto unit-testea por
  diseño. `Api` e `Infrastructure` están intencionalmente en 0% en este
  reporte porque se validan con integration tests (HTTP + base de datos real),
  no con mocks — ver Tarea 1 y Tarea 2 para el razonamiento completo.
- Las 88 pruebas heredadas de la Tarea 1 siguen nombradas en inglés; las 33
  nuevas de esta actividad (`SimulationDataServiceTests.cs`, `DtosTests.cs`) y
  todo lo de integración/contrato ya están en español, siguiendo la
  convención de `CLAUDE.md`. Sigue siendo una inconsistencia heredada, no
  resuelta a propósito para no generar un diff masivo sin valor funcional.
