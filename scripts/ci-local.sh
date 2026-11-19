#!/usr/bin/env bash
# Corre en local los mismos pasos del job build-and-test de .github/workflows/ci.yml.
# Uso: pnpm ci:local   (o bash scripts/ci-local.sh)
set -euo pipefail
cd "$(dirname "$0")/.."

SLN=Nurtricenter.MS3.sln
paso() { printf '\n==> %s\n' "$1"; }

paso "Restore";      dotnet restore "$SLN"
paso "Lint";         dotnet format whitespace "$SLN" --verify-no-changes
paso "Build";        dotnet build "$SLN" -c Release --no-restore -nologo
for p in Nurtricenter.MS3.Tests Nurtricenter.MS3.IntegrationTests \
         Nurtricenter.MS3.PactTests.Consumer Nurtricenter.MS3.PactTests.Provider; do
	paso "Pruebas: $p"
	dotnet test "$p/$p.csproj" -c Release --no-build -nologo
done
printf '\nCI local OK\n'
