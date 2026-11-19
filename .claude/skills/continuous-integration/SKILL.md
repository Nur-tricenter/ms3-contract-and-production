---
name: continuous-integration
description: Contrato del ciclo de continuous integration del microservicio Nurtricenter MS3 (Husky, lint-staged, GitHub Actions y SonarCloud). Úsalo siempre antes de tocar los hooks de git, lint-staged.config.js, .github/workflows/ci.yml o la configuración de Sonar, cuando un commit o un push sea rechazado por un hook, o cuando falle un job del pipeline.
---

# Continuous integration — Nurtricenter MS3

Este ciclo ya está construido. No lo rediseñes: ajústalo dentro de estas reglas.

## Piezas

| Pieza | Ruta | Rol |
|---|---|---|
| Husky | `.husky/pre-commit`, `.husky/pre-push` | Hooks de git versionados. `pnpm install` los activa (script `prepare`). |
| lint-staged | `lint-staged.config.js` | Corre el linter solo sobre los archivos en stage. |
| Linter / formateador | `dotnet format whitespace` + `.editorconfig` | `.cs` con tabs; YAML, JSON y Markdown con espacios. |
| Analizador | `SonarAnalyzer.CSharp` en `Directory.Build.props` | Reglas de Sonar en cada build (advertencias). |
| Pipeline | `.github/workflows/ci.yml` | Jobs `build-and-test` y `sonarcloud`. |
| Instalación automática | Target `InstalarHooksGit` en `Directory.Build.props` | Hace `pnpm install` en el primer build local si falta `node_modules`. No corre con `CI=true` ni con `HUSKY=0`. |

## Ciclo

```
git commit  ->  .husky/pre-commit  ->  lint-staged  ->  dotnet format whitespace --verify-no-changes --include <.cs en stage>
git push    ->  .husky/pre-push    ->  dotnet build + pruebas unitarias
GitHub      ->  ci.yml             ->  restore, lint, build, unitarias, integración, Pact consumer, Pact provider
                                   ->  SonarCloud: begin, build, pruebas con cobertura OpenCover, end
```

## Comandos

```bash
# Instalar los hooks (una vez por clon)
pnpm install

# Lint igual que el pre-commit, sobre toda la solución
pnpm lint            # dotnet format whitespace Nurtricenter.MS3.sln --verify-no-changes
pnpm lint:fix        # corrige el formato

# Mismos pasos que el job build-and-test
dotnet restore Nurtricenter.MS3.sln
dotnet format whitespace Nurtricenter.MS3.sln --verify-no-changes
dotnet build Nurtricenter.MS3.sln -c Release --no-restore
dotnet test Nurtricenter.MS3.sln -c Release --no-build

# Saltar hooks en un caso puntual
HUSKY=0 git push        # o git commit --no-verify
```

## Reglas

- El linter del pre-commit es `dotnet format whitespace`. No agregues `dotnet format style` ni `analyzers` al pre-commit: compilan la solución y el commit tarda más de un minuto.
- lint-staged entrega rutas absolutas; `lint-staged.config.js` las convierte a relativas porque `--include` las espera así. No quites esa conversión.
- Los hooks llevan finales de línea LF (`.gitattributes`). Si se guardan con CRLF, Git Bash en Windows falla con `/usr/bin/env: 'sh\r'`.
- Todo paso nuevo del pipeline debe poder correrse en local con el mismo comando. No uses acciones de terceros para compilar o probar.
- El job `sonarcloud` necesita el secreto `SONAR_TOKEN_TEST`. La organización y la clave del proyecto salen de las variables `SONAR_ORGANIZATION` y `SONAR_PROJECT_KEY`, con `nur-tricenter` y `ms3-contract-and-production` por defecto.
- En SonarCloud, **Automatic Analysis** debe estar desactivado. Si está activo, `sonarscanner end` falla.
- La cobertura que ve Sonar sale de `Nurtricenter.MS3.Tests` y `Nurtricenter.MS3.IntegrationTests` en formato OpenCover. Las pruebas Pact no suman cobertura.
- `coverage-report/`, `topology/` y las migraciones están excluidos del análisis. Si agregas código generado, exclúyelo en `sonar.exclusions` del paso *Iniciar análisis*.

## Diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| El commit se rechaza con `error WHITESPACE: Fix whitespace formatting` | Un `.cs` en stage tiene espacios en vez de tabs | `pnpm lint:fix`, `git add` y volver a commitear |
| `lint-staged: command not found` o el hook no corre | Falta `pnpm install` en este clon | `pnpm install` |
| `dotnet: not found` dentro del hook | El SDK no está en el `PATH` de Git Bash | Abrir una terminal nueva después de instalar el SDK de .NET 8 |
| Job `build-and-test` falla en *Lint* | Alguien commiteó con `--no-verify` | `pnpm lint:fix` y commitear |
| Job `sonarcloud` falla en *Verificar SONAR_TOKEN* | Falta el secreto | Crearlo en *Settings → Secrets and variables → Actions* |
| `sonarscanner end` dice que Automatic Analysis está activo | Los dos métodos de análisis a la vez | Desactivar Automatic Analysis en SonarCloud |
| Sonar muestra 0 % de cobertura | No se generó `coverage.opencover.xml` | Revisar que el `dotnet test` del job tenga `--collect:"XPlat Code Coverage"` y el `Format=opencover` |
