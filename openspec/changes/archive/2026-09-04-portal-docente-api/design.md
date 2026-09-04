## Context

El frontend de Portal ya define el perfil y sus ocho secciones, pero `Modules.Portal` solo tiene `ping`, un `DbContext` vacío y un migrador. La identidad canónica se obtiene desde `ArsDocendi.Shared`/`identity`; el módulo Portal debe persistir únicamente los datos que el docente administra y exponerlos a través de su API.

## Goals / Non-Goals

**Goals:**

- Reemplazar el mock por una lectura y escritura HTTP persistente, manteniendo la forma de datos que ya consume la pantalla.
- Aislar autorización por persona autenticada y evitar que un docente acceda a datos ajenos.
- Incorporar DDL SQL embebido en la migración existente, auditoría y fixtures reproducibles.
- Dejar Contracts suficientemente puros para futuros consumidores de consultas.

**Non-Goals:**

- Almacenamiento, descarga o análisis de archivos binarios.
- Vista administrativa de todos los perfiles o moderación de habilidades.
- Lectura de horas/designaciones o dependencia hacia `Modules.Designaciones`.
- Cambios en el login, Azure AD o en la propiedad administrativa de `identity`.

## Decisions

### 1. Un agregado de perfil por persona y tablas de sección

Crear una fila raíz `portal.perfiles` vinculada por `persona_id` como referencia blanda a `identity.personas`. Las secciones simples se guardan en `portal.contactos` y `portal.cvs`; las colecciones usan tablas propias (`experiencias`, `educaciones`, `certificaciones`, `proyectos` y `proyecto_documentos`). Todas incluyen `created_at` y `audit.attach`.

Se eligen tablas normalizadas en lugar de un JSON completo porque cada ítem necesita CRUD, validación, auditoría y consultas independientes. El contacto administrado por el docente vive en Portal; no se escribe `identity.personas`, cuya escritura sigue restringida a Administración.

### 2. API orientada al perfil autenticado

El endpoint raíz será `GET /api/portal/perfil`. Las mutaciones se separan por sección:

- `PUT /api/portal/perfil/contacto`
- `PUT` y `DELETE /api/portal/perfil/cv`
- `POST`, `PUT`, `DELETE /api/portal/perfil/experiencia/{id}`
- `POST`, `PUT`, `DELETE /api/portal/perfil/educacion/{id}`
- `POST`, `PUT`, `DELETE /api/portal/perfil/certificaciones/{id}`
- `POST`, `PUT`, `DELETE /api/portal/perfil/proyectos/{id}`
- `PUT /api/portal/perfil/habilidades` y `PUT /api/portal/perfil/intereses`

No se acepta `personaId` desde el cliente. El servicio resuelve el usuario actual mediante `IConsultasIdentity`, obtiene la persona vinculada y pasa ese identificador al repositorio. Un ítem que no pertenece a esa persona se trata como no encontrado para no filtrar su existencia.

### 3. Capas y contratos

Los DTOs de request/response y la interfaz de consultas públicas viven en `Modules.Portal.Contracts`; no contienen lógica. El módulo implementa `Controller -> Service -> Repository`, con validaciones en Service y persistencia en Repository. Los controladores usan las políticas de autenticación existentes y el manejador común de errores/problem details.

### 4. Tags con vocabulario compartido

`portal.habilidades` contiene el término normalizado único, el texto visible, contador de usos y eventual alias canónico. `portal.docente_habilidades` relaciona perfil, término y `tipo` (`habilidad`/`interes`) con una clave única compuesta. La normalización se aplica en el servicio y la unicidad se refuerza en PostgreSQL; la operación de reemplazo de cada lista se ejecuta en una transacción.

### 5. Archivos como metadata

CV y documentos de proyectos guardan nombre, fecha y, cuando corresponda, una URI `synthetic://` o futura referencia de storage. La API valida extensión/tipo declarado como PDF, pero no recibe bytes ni expone descarga. Un CV se modela como una única relación vigente, por lo que reemplazarlo actualiza esa fila.

### 6. Migración y seed

Los SQL se agregan bajo `database/portal/` y se embeben en la nueva migración de `PortalDbContext`, siguiendo el orden de migraciones existente. El seed se agrega al final de `infra/scripts/seed-data/sintetico.sql`, con UUIDs reservados, `ON CONFLICT` y al menos un perfil completo, uno parcial y uno vacío. No se agregan edges al grafo: Portal sigue dependiendo solo de Shared y de sus Contracts.

### 7. Frontend

Crear `features/portal/api/` con funciones del `apiClient` y reemplazar el hook/store mock por `useQuery` y mutaciones React Query. Los componentes conservarán sus tipos y comportamiento visual; después de cada mutación se invalida la query del perfil. El mock se mantiene solo para tests aislados durante la transición y deja de ser importado por la pantalla.

## Risks / Trade-offs

- [Los adjuntos no son descargables] -> Se documenta como límite de esta versión y se conserva una URI/metadata compatible con un futuro storage.
- [El perfil completo requiere varias tablas y joins] -> El repositorio arma una única respuesta agregada y usa transacciones solo en operaciones que actualizan varias relaciones.
- [La normalización de términos puede considerar equivalentes textos que el equipo quería distinguir] -> Se conserva el texto original y `canonica_id` permite corregir equivalencias desde administración en una iteración posterior.
- [Fixtures con perfiles mock no coinciden exactamente con todas las identidades] -> El seed usa los mismos UPN/persona UUID que el frontend y cubre estados completo/parcial/vacío.

## Migration Plan

1. Desplegar el backend con la migración `portal` y ejecutar el seed no productivo.
2. Verificar `GET /api/portal/perfil` con las identidades de desarrollo y ejecutar pruebas de mutación/aislamiento.
3. Desplegar el frontend que usa la API.
4. Para rollback, revertir el frontend al adaptador mock si fuera necesario y revertir el release backend; no borrar tablas ni filas de usuarios fuera de las fixtures.
