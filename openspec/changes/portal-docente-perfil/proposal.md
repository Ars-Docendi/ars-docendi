## Why

El Departamento carga hoy manualmente la información de cada docente y la recibe en formatos sueltos —un CV en PDF por mail, un título escaneado, un dato de contacto desactualizado—, lo que satura a Secretaría y deja la información vieja apenas se carga. El módulo Portal existe scaffolded pero vacío: `/portal` es la pantalla a la que redirige la raíz del sistema y hoy muestra "Módulo en construcción".

Este change entrega la **cara del docente** del Portal: el CV del docente convertido en **datos consultables**, mantenidos por él mismo. Es la primera pantalla que ve un docente al ingresar al sistema, así que además de reducir carga administrativa define la primera impresión de Ars Docendi.

## What Changes

- La pantalla **"Mi Portal"** (`/portal`) deja de ser un placeholder y pasa a mostrar el perfil del docente autenticado, con estados Loading / Empty / Error / Success sobre un store mock local.
- **Ocho secciones en una sola página, sin pestañas**: Perfil (read-only), Contacto, CV, Experiencia, Educación, Certificaciones, Proyectos, y Habilidades e Intereses.
- **Modelo de interacción "perfil vivo"**: lectura por defecto y edición **por sección**, cada una con su propio guardado. No hay un "Guardar" global al pie, nada es obligatorio y nada bloquea — a diferencia del formulario de pedido de designación, que es transaccional.
- **Sin copy explicativo**: la interfaz se entiende por su forma y sus etiquetas. Lo read-only se comunica por ausencia de afordancia de edición, no con texto.
- **Las secciones vacías ocupan una línea, no una tarjeta vacía**; al llenarse se expanden. La página crece con el perfil, así que el avance es visible sin barra de progreso ni avisos de "te falta cargar X".
- **Ownership por campo, una sola fuente por campo**: el docente es dueño de contacto, CV, experiencia, educación, certificaciones, proyectos, habilidades e intereses; Secretaría es dueña de DNI, legajo y CUIL; Azure AD provee identidad y mail institucional. Sin duplicar campos que ya viven en `features/docentes`.
- **Prototipo con datos mockeados**, mismo patrón validado en `admin-docentes` y `pedidos-designacion`: sin backend, sin HTTP real, sin Contracts nuevos.
- **Fuera de alcance explícito**: declaración y conciliación de horas, cualquier vínculo con Designaciones, la vista de Secretaría (búsqueda por habilidad), el onboarding paso a paso, la persistencia real y los workflows de validación de títulos.

## Capabilities

### New Capabilities

- `perfil-docente-portal`: La pantalla "Mi Portal" del docente autenticado — ruta, estados de carga, composición de secciones en una página, el patrón transversal de lectura/edición por sección y de sección vacía, y el bloque **Perfil** read-only (identidad de Azure AD + datos institucionales de Secretaría). Base del store mock del perfil.
- `datos-contacto-docente`: Edición del teléfono y el mail de contacto del docente. El mail institucional permanece read-only en Perfil.
- `cv-docente`: Carga y reemplazo del CV en PDF como archivo único del perfil, con la sección vacía presentada como dropzone.
- `formacion-docente`: Educación (nivel, carrera o título, institución, período) y certificaciones (nombre, emisor, fecha, vencimiento opcional) como listas administrables. Informativas, sin workflow de aprobación.
- `trayectoria-docente`: Experiencia laboral (puesto, organización, período, descripción) y proyectos (nombre, rol, años, descripción, PDF o DOI) como listas administrables. Proyectos **incluye** las investigaciones y sus documentos.
- `experticia-docente`: Habilidades e intereses como dos listas de tags separadas, desde vocabulario compartido con opción de sugerir uno nuevo.

### Modified Capabilities

_(ninguna — todo es nuevo; no cambian requisitos de specs existentes.)_

## Impact

- **Frontend** (`frontend/src/features/portal/`): se llena el slice ya existente — `types.ts` (hoy `export {}`), `pages/`, más `mock/mockStore.ts`, `components/` y `hooks/`. Sin llamadas HTTP reales.
- `frontend/src/app/router.tsx` y `shell/nav.ts`: la ruta `/portal` y la entrada "Mi Portal" ya existen; solo se verifican.
- **Backend**: sin cambios. `Modules.Portal` y `Modules.Portal.Contracts` no se tocan. `IPortalQueries` sigue siendo un placeholder.
- **Grafo de dependencias**: sin edges nuevos. `Modules.Portal` queda hoja del DAG — al no manejar horas, el Portal no necesita leer nada de Designaciones, y el edge proyectado `Designaciones → Portal.Contracts` no se activa en este change.
- **Componentes**: todo sale de `@ars-docendi/ui` salvo el widget de tags de habilidades/intereses (la librería no tiene Tag/Chip/Combobox/MultiSelect). Se compone a nivel feature siguiendo el precedente de `MateriasSelector` y `AsignacionesSelector`, y se registra la deuda en `docs/quality/tech-debt.md`.
- **Docs**: al ser prototipo mock sin cambio de schema ni de API, no aplica actualizar `data-model.md` ni `api-contracts.md`. Sí se produce el design spec `docs/product/designs/portal-docente-design-spec.md` (invariante #12).
- **Reglas de negocio**: ninguna proviene de normativa institucional, así que este change no agrega `BR-portal-NNN`. Si al validar con Secretaría aparece alguna regla reglamentaria, se registra con su cita en `docs/business-rules/portal.md`.
- **Rollback**: revertir el PR. No hay estado persistido ni migraciones — todo el estado vive en memoria en el store mock.
- **Cabo suelto ajeno a este change**: `frontend/src/features/designaciones/types.ts:135` documenta `horasInvestigacion` como dato que vendría del Portal vía cross-module. Con este alcance el Portal no maneja horas, así que ese comentario quedó desactualizado y conviene corregirlo aparte.
