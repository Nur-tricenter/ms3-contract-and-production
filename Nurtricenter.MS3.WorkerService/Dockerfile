# Se construye con la carpeta Nurtricenter/ como contexto:
#   docker build -f Nurtricenter.MS3.WorkerService/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Nurtricenter.MS3.WorkerService/Nurtricenter.MS3.WorkerService.csproj", "Nurtricenter.MS3.WorkerService/"]
COPY ["Nurtricenter.MS3.Core/Nurtricenter.MS3.Core.csproj", "Nurtricenter.MS3.Core/"]
COPY ["Nurtricenter.MS3.Application/Nurtricenter.MS3.Application.csproj", "Nurtricenter.MS3.Application/"]
COPY ["Nurtricenter.MS3.Infrastructure/Nurtricenter.MS3.Infrastructure.csproj", "Nurtricenter.MS3.Infrastructure/"]
RUN dotnet restore "Nurtricenter.MS3.WorkerService/Nurtricenter.MS3.WorkerService.csproj"

COPY . .
RUN dotnet publish "Nurtricenter.MS3.WorkerService/Nurtricenter.MS3.WorkerService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Nurtricenter.MS3.WorkerService.dll"]
