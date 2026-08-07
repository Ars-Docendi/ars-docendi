# syntax=docker/dockerfile:1
#
# Imagen del backend Ars Docendi (ArsDocendi.Host).
#
# Contexto de build = RAÍZ del repo, no `backend/`:
#   docker build -f backend/Dockerfile -t <tag> .
#
# El motivo es que los .csproj del backend embeben el DDL versionado de
# `database/` como recurso del assembly (ver data-model.md → Migraciones). Ese
# directorio es un input de compilación, así que tiene que estar dentro del
# contexto. Con el contexto en `backend/` los globs no matcheaban nada, el
# assembly compilaba sin recursos y las migraciones fallaban recién en el deploy.
# Los .csproj ahora fallan el build si el glob viene vacío.

# --- Stage build: SDK .NET 10 ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json fija la versión del SDK; copiarlo primero para que el restore
# la respete antes de traer el resto del código.
COPY backend/global.json ./backend/
COPY backend/ ./backend/

# DDL versionado: input de compilación, no artefacto de runtime. La ruta relativa
# tiene que quedar igual que en el repo, porque los .csproj lo referencian con
# ..\..\..\database desde backend/src/<Proyecto>/.
COPY database/ ./database/

RUN dotnet restore backend/ArsDocendi.slnx
RUN dotnet publish backend/src/ArsDocendi.Host/ArsDocendi.Host.csproj \
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
