# ETAPA 1 — build (SDK para compilar)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copio solo los .csproj y restauro primero (aprovecha cache de capas)
COPY ["Nurtricenter.Api/Nurtricenter.MS3.Api.csproj", "Nurtricenter.Api/"]
COPY ["Nurtricenter.MS3.Core/Nurtricenter.MS3.Core.csproj", "Nurtricenter.MS3.Core/"]
COPY ["Nurtricenter.MS3.Application/Nurtricenter.MS3.Application.csproj", "Nurtricenter.MS3.Application/"]
COPY ["Nurtricenter.MS3.Infrastructure/Nurtricenter.MS3.Infrastructure.csproj", "Nurtricenter.MS3.Infrastructure/"]
RUN dotnet restore "Nurtricenter.Api/Nurtricenter.MS3.Api.csproj"

# Copio el resto del código y publico en Release
COPY . .
RUN dotnet publish "Nurtricenter.Api/Nurtricenter.MS3.Api.csproj" -c Release -o /app/publish --no-restore

# ETAPA 2 — runtime (solo lo necesario para correr)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Nurtricenter.MS3.Api.dll"]
