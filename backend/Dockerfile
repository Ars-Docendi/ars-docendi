# syntax=docker/dockerfile:1
#
# Imagen del backend Ars Docendi (ArsDocendi.Host).
# Contexto de build = carpeta `backend/` (los workflows corren `docker build … backend`).

# --- Stage build: SDK .NET 10 ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json fija la versión del SDK; copiarlo primero para que el restore
# la respete antes de traer el resto del código.
COPY global.json ./
COPY . .

RUN dotnet restore ArsDocendi.slnx
RUN dotnet publish src/ArsDocendi.Host/ArsDocendi.Host.csproj \
    -c Release -o /app --no-restore

# --- Stage runtime: ASP.NET 10, no-root ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Usuario no-root provisto por la imagen base de .NET (APP_UID).
USER $APP_UID

# Coincide con ASPNETCORE_URLS=http://+:8080 que inyecta compose.base.yml.
EXPOSE 8080

# CMD (no ENTRYPOINT): spin-up.sh reemplaza el comando entero para correr
# `dotnet ArsDocendi.Host.dll --migrate`. Con ENTRYPOINT el override se
# concatenaría y rompería el comando (ver design.md, decisión D-cmd).
CMD ["dotnet", "ArsDocendi.Host.dll"]
