# CLAUDE.md — Nurtricenter MS3 (Contracting & Production)

Microservicio .NET 8 con arquitectura por capas (Core, Application,
Infrastructure, Api, WorkerService). Mensajería con RabbitMQ y PostgreSQL en
ejecución real; las pruebas corren sin Docker (SQLite y broker en memoria).

## Comandos

```bash
npm install                                   # una vez: instala Husky + lint-staged
dotnet build Nurtricenter.MS3.sln
dotnet test Nurtricenter.MS3.sln              # unitarias + integración + Pact
npm run lint                                  # formato, igual que el pre-commit
npm run lint:fix
```

## Skills

Carga el skill que corresponda antes de trabajar en su área:

| Área | Skill |
|---|---|
| Pruebas unitarias | `unit-testing` |
| Pruebas de integración | `integration-testing` |
| Pruebas de contrato (Pact) | `pact-testing` |
| Flujos de negocio y payloads | `test-flows` |
| Hooks, pipeline de GitHub Actions y SonarCloud | `continuous-integration` |

## Agentes

- `test-writer` / `test-verifier`: pruebas unitarias.
- `integration-test-writer` / `integration-test-verifier`: pruebas de integración.
- `pact-writer` / `pact-checker`: pruebas de contrato.
- `ci-verifier`: revisa cambios a la configuración de CI o un job fallido.

Lo que escribe un agente lo revisa otro que no lo escribió.

## Reglas

- Los `.cs` se indentan con tabs (`.editorconfig`). El pre-commit rechaza el
  commit si no; no lo saltes con `--no-verify` salvo que el usuario lo pida.
- Todo cambio debe pasar `dotnet build` y `dotnet test` en local antes del push:
  el pipeline corre los mismos comandos.
- No pongas tokens ni cadenas de conexión en archivos versionados. Los
  `appsettings*.json` están en `.gitignore`; usa las plantillas `*.template.json`.
- Commits en inglés con prefijo convencional (`feat:`, `fix:`, `chore:`, `ci:`, `docs:`).
