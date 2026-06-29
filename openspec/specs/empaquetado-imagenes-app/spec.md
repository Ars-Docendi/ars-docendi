# empaquetado-imagenes-app

## Purpose

Define cómo se empaqueta la aplicación Ars Docendi en imágenes de contenedor (backend y frontend) y los contratos asociados de arranque: migraciones one-shot idempotentes vía `--migrate`, fallback SPA sin secuestrar `/api`, y el nombre consistente de connection string entre la app y la infraestructura (compose/Traefik por ambiente).

## Requirements

### Requirement: Imagen de contenedor del backend

El repo SHALL proveer un `backend/Dockerfile` que produzca una imagen ejecutable del `ArsDocendi.Host` construida con el SDK de .NET 10 y corrida sobre la imagen runtime de ASP.NET 10. La imagen MUST exponer el puerto `8080` (coincidiendo con `ASPNETCORE_URLS=http://+:8080` que inyecta `compose.base.yml`), correr como usuario no-root, y usar `backend/` como contexto de build (los workflows invocan `docker build … backend`). Un `backend/.dockerignore` MUST excluir `bin/`, `obj/` y demás artefactos locales para que el contexto sea determinístico.

#### Scenario: Build del backend desde el contexto del repo

- **WHEN** se ejecuta `docker build -t arsdocendi-backend backend` sobre el repo limpio
- **THEN** el build completa sin error
- **AND** la imagen resultante arranca el web server escuchando en el puerto 8080

#### Scenario: Contexto de build sin artefactos locales

- **WHEN** existe `backend/src/**/bin` y `backend/src/**/obj` en el working tree
- **THEN** el `.dockerignore` los excluye del contexto enviado al daemon
- **AND** el build no depende de binarios precompilados del host

#### Scenario: La imagen no corre como root

- **WHEN** se inspecciona el usuario efectivo de la imagen del backend
- **THEN** NO es `root`

### Requirement: Imagen de contenedor del frontend

El repo SHALL proveer un `frontend/Dockerfile` multi-stage que (1) ejecute `pnpm install` + `pnpm build` con Node, resolviendo la dependencia `@ars-docendi/ui` (referencia `github:`) durante la instalación, y (2) sirva el `dist/` resultante con nginx en el puerto `80` (coincidiendo con la label de Traefik en `compose.base.yml`). El contexto de build MUST ser `frontend/`, con un `frontend/.dockerignore` que excluya `node_modules/` y `dist/`.

#### Scenario: Build del frontend desde el contexto del repo

- **WHEN** se ejecuta `docker build -t arsdocendi-frontend frontend` sobre el repo limpio
- **THEN** el build resuelve `@ars-docendi/ui` desde GitHub, compila el bundle de Vite, y completa sin error
- **AND** la imagen resultante sirve los estáticos en el puerto 80

### Requirement: Fallback SPA sin secuestrar /api

La imagen del frontend SHALL servir la aplicación como Single Page Application: cualquier ruta desconocida MUST devolver `index.html` (fallback para react-router). El nginx del frontend NO SHALL proxear ni interceptar rutas bajo `/api` — ese ruteo lo resuelve Traefik hacia el backend del mismo ambiente (D5 de `ephemeral-environments`).

#### Scenario: Ruta de cliente desconocida

- **WHEN** un usuario navega a una ruta de cliente (p. ej. `/designaciones/123`) servida por la imagen del frontend
- **THEN** nginx responde `index.html` con 200
- **AND** react-router resuelve la vista en el cliente

#### Scenario: El frontend no resuelve /api

- **WHEN** llega un request bajo `/api` al contenedor del frontend
- **THEN** la imagen del frontend NO lo resuelve como estático ni lo proxea
- **AND** el ruteo de `/api` queda delegado a Traefik (PathPrefix `/api` → backend)

### Requirement: Migraciones one-shot idempotentes vía --migrate

El `ArsDocendi.Host` SHALL reconocer el argumento de línea de comandos `--migrate`. Al recibirlo, MUST aplicar las migraciones pendientes de cada uno de los 4 módulos (Designaciones, Aulas, Portal, Tareas) y terminar el proceso con exit code 0 **sin** levantar el web server. La operación MUST ser idempotente: re-ejecutarla sobre una base ya migrada no produce error ni cambios. Cada módulo SHALL exponer su rutina de migración a través de su proyecto `*.Contracts`; el Host NO SHALL referenciar los `*DbContext` internos de los módulos (invariante #1: cross-module solo vía Contracts).

#### Scenario: Migrar una base nueva

- **WHEN** se corre el backend con `--migrate` (p. ej. `dotnet ArsDocendi.Host.dll --migrate`) contra una base vacía del ambiente
- **THEN** se aplican las migraciones de los 4 módulos
- **AND** el proceso termina con exit code 0 sin abrir el listener HTTP

#### Scenario: Re-ejecutar migraciones es idempotente

- **WHEN** se corre `--migrate` por segunda vez sobre una base ya migrada
- **THEN** no se aplica ninguna migración nueva
- **AND** el proceso termina con exit code 0 sin error

#### Scenario: Arranque normal sin el argumento

- **WHEN** el backend arranca sin `--migrate`
- **THEN** levanta el web server normalmente y NO ejecuta el flujo de migración one-shot

#### Scenario: La migración respeta la frontera de módulos

- **WHEN** se inspeccionan las referencias del proyecto `ArsDocendi.Host`
- **THEN** invoca la migración de cada módulo solo a través de su `Modules.<X>.Contracts`
- **AND** NO referencia ningún tipo de `Modules.<X>.Infrastructure` interno

### Requirement: Nombre de connection string consistente entre app e infra

La aplicación SHALL leer su connection string de PostgreSQL bajo la clave `ArsDocendi` (`ConnectionStrings:ArsDocendi`), de modo que coincida con la variable `ConnectionStrings__ArsDocendi` que `compose.base.yml` y `spin-up.sh` ya inyectan por ambiente. Los 4 `ModuleExtensions.cs` y el `appsettings.json` MUST usar ese mismo nombre.

#### Scenario: Backend deployado resuelve su base

- **WHEN** un ambiente arranca el backend con `ConnectionStrings__ArsDocendi` apuntando a su base aislada
- **THEN** los 4 módulos resuelven esa connection string
- **AND** ninguno lee una clave inexistente que devuelva null

#### Scenario: Desarrollo local sigue funcionando

- **WHEN** un developer corre el Host localmente con el `appsettings.json` del repo
- **THEN** la clave `ConnectionStrings:ArsDocendi` provee la cadena de conexión local por defecto
