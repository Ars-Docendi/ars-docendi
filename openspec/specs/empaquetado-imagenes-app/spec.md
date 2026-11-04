# empaquetado-imagenes-app

## Purpose

Define cómo se empaqueta la aplicación Ars Docendi en imágenes de contenedor (backend y frontend) y los contratos asociados de arranque: migraciones one-shot idempotentes vía `--migrate`, fallback SPA sin secuestrar `/api`, y el nombre consistente de connection string entre la app y la infraestructura (compose/Traefik por ambiente).

## Requirements

### Requirement: Imagen de contenedor del backend

El repo SHALL proveer un `backend/Dockerfile` que produzca una imagen ejecutable del `ArsDocendi.Host` construida con el SDK de .NET 10 y corrida sobre la imagen runtime de ASP.NET 10. La imagen MUST exponer el puerto `8080` (coincidiendo con `ASPNETCORE_URLS=http://+:8080` que inyecta `compose.base.yml`), correr como usuario no-root, y usar la raíz del repo como contexto para incluir el DDL de `database/`. El `.dockerignore` raíz MUST excluir `bin/`, `obj/` y demás artefactos locales para que el contexto sea determinístico.

#### Scenario: Build del backend desde el contexto del repo

- **WHEN** se ejecuta `docker build -t arsdocendi-backend -f backend/Dockerfile .` sobre el repo limpio
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

El repo SHALL proveer un `frontend/Dockerfile` multi-stage que (1) instale el workspace con el lockfile raíz en modo `--frozen-lockfile` y compile el paquete `frontend`, resolviendo `@ars-docendi/ui`, y (2) sirva el `dist/` resultante con nginx en el puerto `80`. El contexto de build MUST ser la raíz del repo para usar su único lockfile; el `.dockerignore` raíz excluye dependencias y artefactos locales.

#### Scenario: Build del frontend desde el contexto del repo

- **WHEN** se ejecuta `docker build -t arsdocendi-frontend -f frontend/Dockerfile .` sobre el repo limpio
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

El `ArsDocendi.Host` SHALL reconocer `--migrate` y aplicar las migraciones pendientes de Identity/Audit, Storage y los módulos Designaciones, Aulas, Portal y Tareas que tengan migraciones disponibles. MUST respetar dependencias y terminar con exit code 0 sin levantar listener sólo cuando todas las operaciones terminen correctamente. Re-ejecutar sobre historia compatible ya actualizada MUST no alterar datos/schema. El Host SHALL coordinar mediante un contrato transversal puro sin referenciar DbContexts ni implementaciones internas de otros módulos. Ante historia incompatible o falla MUST devolver exit no cero sin aparentar éxito; MUST NOT prometer atomicidad global entre contextos.

#### Scenario: Migrar una base nueva

- **WHEN** se ejecuta el backend con `--migrate` contra una base vacía
- **THEN** MUST aplicarse Identity/Audit y Storage antes de sus consumidores
- **AND** MUST terminar sin abrir el listener HTTP

#### Scenario: Re-ejecutar migraciones es idempotente

- **WHEN** se ejecuta `--migrate` por segunda vez sobre una base compatible ya migrada
- **THEN** no se aplica ninguna migración nueva ni se modifica el dataset
- **AND** el proceso termina con exit 0

#### Scenario: Arranque normal sin el argumento

- **WHEN** el backend arranca sin modo de migraciones
- **THEN** levanta el web server normalmente y NO aplica migraciones automáticamente

#### Scenario: La migración respeta la frontera de módulos

- **WHEN** se inspeccionan las referencias del Host
- **THEN** las operaciones de migración MUST cruzar sólo contratos públicos sin usar DbContexts internos

#### Scenario: Falla en un contexto

- **GIVEN** un contexto falla después de que otro ya confirmó una migración
- **WHEN** finaliza el runner
- **THEN** MUST devolver error identificando el contexto sin registrar éxito global ni asumir rollback de los anteriores

### Requirement: Nombre de connection string consistente entre app e infra

La aplicación SHALL leer su connection string de PostgreSQL bajo la clave `ArsDocendi` (`ConnectionStrings:ArsDocendi`), de modo que coincida con la variable `ConnectionStrings__ArsDocendi` que `compose.base.yml` y `spin-up.sh` ya inyectan por ambiente. Los 4 `ModuleExtensions.cs` y el `appsettings.json` MUST usar ese mismo nombre.

#### Scenario: Backend deployado resuelve su base

- **WHEN** un ambiente arranca el backend con `ConnectionStrings__ArsDocendi` apuntando a su base aislada
- **THEN** los 4 módulos resuelven esa connection string
- **AND** ninguno lee una clave inexistente que devuelva null

#### Scenario: Desarrollo local sigue funcionando

- **WHEN** un developer corre el Host localmente con el `appsettings.json` del repo
- **THEN** la clave `ConnectionStrings:ArsDocendi` provee la cadena de conexión local por defecto

### Requirement: Estado de migraciones consultable sin escritura

El backend SHALL ofrecer un modo one-shot `--estado-migraciones` con JSON estable que identifique contexto, migraciones disponibles/aplicadas/pendientes, destino seguro y compatibilidad. MUST no escribir en la base, crear histories/schemas ni abrir listener. MUST no incluir credenciales, connection strings ni secretos de storage. Estado incompatible MUST devolver error no cero.

#### Scenario: Consulta sobre base vacía o actualizada

- **GIVEN** una base vacía o compatible completamente migrada
- **WHEN** se consulta su estado
- **THEN** MUST informar respectivamente el baseline pendiente o ausencia de pendientes sin efectos sobre la base

#### Scenario: Historial incompatible

- **GIVEN** identificadores aplicados no reconocidos o un historial discontinuo
- **WHEN** se consulta el estado
- **THEN** MUST indicarse incompatibilidad y terminar con error sin writes

### Requirement: Preview SQL ligado al estado observado

El backend SHALL ofrecer `--script-migraciones <directorio>` para generar SQL exclusivamente del intervalo pendiente por contexto y un manifiesto con orden, identidad de release y estado observado. MUST usar recursos embebidos, no escribir en la base ni abrir listener, y rechazar rutas inseguras. MUST identificar ausencia de cambios y conservar las fronteras entre scripts/histories de contextos. El preview MUST NOT presentarse como comparación automática del schema real ni como script válido para cualquier estado posterior.

#### Scenario: Generación con cambios pendientes

- **GIVEN** una base compatible con migraciones pendientes
- **WHEN** se genera el preview
- **THEN** los archivos MUST representar sólo esas migraciones y el manifiesto MUST ordenar sus dependencias

#### Scenario: Base ya actualizada

- **GIVEN** ninguna migración pendiente
- **WHEN** se genera el preview
- **THEN** el manifiesto MUST indicar no-op sin inventar SQL de actualización

#### Scenario: Cambio de estado antes de aplicar

- **GIVEN** un preview generado para un historial observado
- **WHEN** el historial cambia antes de la aplicación
- **THEN** el despliegue MUST revalidar y no ejecutar ciegamente el artefacto anterior
