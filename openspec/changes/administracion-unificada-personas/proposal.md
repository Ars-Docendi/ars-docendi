# Proposal

## Why

Hoy una misma persona se administra en dos pantallas: **Usuarios** (identidad, cuenta y roles) y **Docentes** (membresías docentes y asignaciones), cada una con su modal, sus filtros y su API. El operador tiene que saber en cuál de las dos está cada dato, y lo que el docente carga en **Mi Portal** no se puede consultar desde la administración. Se unifica todo en una operación **Administrar personas**, con alta y edición a pantalla completa, y se suma la consulta (y, para ciertos roles, la corrección) del perfil de Mi Portal.

El diseño se validó con un prototipo interactivo (listado con filtros por encabezado, editor con pestañas, perfil de Mi Portal con la misma estructura que la pantalla del docente y permisos por rol).

## What Changes

### Fase 0 — Buscador de materias (implementada)

- Buscador emergente "Buscar materia" con el formato de `PopupBuscarResponsable`: filtros por código y nombre de materia y de carrera, "Buscar"/Enter, filas clickeables y "Cancelar". Docente y asignaciones eligen materia + carrera; Jefe de Cátedra, la materia canónica con las carreras en que se dicta. Lo no elegible se ve con su motivo.
- El modal "Buscar materia" se estira; los modales de docente y usuario quedaron estirables y ajustados al contenido hasta que la fase de Personas los reemplace por pantallas completas.
- Los catálogos de administración informan `carreraCodigo`, y el de usuarios devuelve el id de la materia canónica en `materiasPlan` (corrección con test de regresión).

### Administrar personas

- **Pantalla nueva `/personas`** que reemplaza a `/usuarios` y `/docentes` (sus rutas redirigen). El menú Configuración muestra **Personas** en lugar de Usuarios y Docentes.
- **Listado** con columnas Nombre, Documento, Legajo, Rol, Asignaciones, Estado y Acciones; filtros por encabezado (`FiltroEncabezado`) y orden por columna como las grillas actuales; fila clickeable; Activar/Desactivar con confirmación.
- **Alta y edición a pantalla completa** (`/personas/nueva`, `/personas/:personaId`) con pestañas **Datos personales** (abre primero) y **Datos docentes** (roles con su ámbito, asignaciones y perfil de Mi Portal). Las asignaciones muestran la materia completa y debajo cargo, dedicación y horas. Usa el buscador de materias existente.
- **Reglas de alta y edición**: toda persona necesita al menos un rol con su ámbito y una cuenta (UPN); las asignaciones son obligatorias si tiene rol Docente o Jefe de Cátedra; el **DNI no puede repetirse** y se avisa mientras se escribe, nombrando a la persona que ya lo tiene. Las personas existentes sin rol o sin cuenta se muestran marcadas y la regla se exige al guardarlas; no se modifican datos existentes.
- **Perfil de Mi Portal** al pie de Datos docentes, con la misma estructura y componentes que la pantalla Mi Portal (Contacto, CV, Experiencia, Educación, Certificaciones, Proyectos, Habilidades, Intereses), con descarga de CV y documentos.
- **Permisos nuevos** (editables desde Roles, asignados por migración):
  - `personas.ver`: ver todas las personas (Administrativo, Secretaría, Decanato, Administrador de Sistemas, Observador del departamento).
  - `personas.ver_ambito`: ver sólo las personas con designación vigente en las materias donde el actor es Jefe de Cátedra.
  - `personas.administrar`: crear, editar datos personales, roles y asignaciones, activar y desactivar. Administrativo, Secretaría Académica, Decanato y Administrador de Sistemas.
  - `portal.administrar`: corregir el Mi Portal de otra persona. Los cuatro anteriores y Jefe de Cátedra (dentro de su ámbito).
  - Se retiran `usuarios.ver`, `usuarios.administrar` y `docentes.ver`, que dejan de tener uso.
- **API**: `GET/POST /api/administracion/personas`, `GET/PUT /{personaId}`, `POST /{personaId}/activar|desactivar`, `GET /catalogos`, `POST /documento/verificar` (verificación de DNI, con el DNI en el body) y `.../{personaId}/portal/...` para leer, descargar y corregir el perfil. **BREAKING**: se retiran `/api/administracion/usuarios` y `/api/administracion/docentes`; el único consumidor es el frontend de esas pantallas, que también se retira.

## Capabilities

### New Capabilities

- `buscador-materias`: buscador emergente de materias por código y carrera para las pantallas administrativas.
- `administrar-personas`: listado, alta, edición, activación y permisos de la administración unificada de personas, incluida la vista y corrección del perfil de Mi Portal.

### Modified Capabilities

- `administracion-identidad-api`: las consultas y mutaciones administrativas pasan a la superficie de personas.
- `perfil-docente-portal`: el perfil puede ser corregido por terceros autorizados, además de su dueño.
- `listar-usuarios`, `crear-usuario`, `modificar-rol-usuario`, `desactivar-usuario`, `listar-docentes`, `crear-docente`, `editar-docente`, `activar-docente`, `desactivar-docente`, `relacion-usuarios-docentes`: sus requisitos de pantalla se retiran; el comportamiento pasa a `administrar-personas`.

## Impact

- **Backend Host**: `ServicioPersonas` + `PersonasController` (generaliza `ServicioDocentes` a todos los roles y compone `ServicioUsuarios`), resolución de ámbito del Jefe de Cátedra (`ResolutorAlcanceDocentes`). Se retiran `UsuariosController`, `DocentesController`, `ServicioDocentes`, `ServicioUsuariosAdministracion` y sus DTOs cuando el frontend migre.
- **Modules.Portal / Portal.Contracts**: nuevo contrato `IAdministracionPortal` para leer y corregir el perfil de una persona dada (el actual sólo opera sobre el usuario autenticado). El Host llama al contrato; no se agrega dependencia Portal → Designaciones.
- **Shared (identity)**: constantes de permisos; `ServicioUsuarios` acepta todos los roles de sistema en la persona.
- **Base de datos**: una migración incremental de `identity` que crea los cuatro permisos, los asigna y retira `usuarios.ver`, `usuarios.administrar` y `docentes.ver`. Sin cambios de schema.
- **Frontend**: feature nueva `personas` (rutas, listado, editor, perfil de Mi Portal reutilizando componentes del portal en modo administración); se retiran `features/usuarios` y `features/docentes`; `nav.ts` y `router.tsx`.
- **Docs**: `api-contracts.md` y `api-contracts-administracion.md`, `dependency-graph.md` (Host → Portal.Contracts para administración), `data-model.md` si lista permisos, design spec `administracion-usuarios-docentes-design-spec.md` (pasa a describir Personas).
- **Fase 0 ya aplicada**: `BuscadorMaterias`, `SelectorMateria` y `busquedaMaterias` en `shared/ui`; `OpcionCatalogoDto.CarreraCodigo`; corrección de `RepositorioUsuarios`; tests de regresión e integración.
- **Rollback**: revertir el diff y deshacer las dos migraciones en orden inverso: `RetirarPermisosUsuariosDocentesIdentity` (su `Down` restaura `usuarios.ver`, `usuarios.administrar` y `docentes.ver` desde los permisos de personas) y después `PermisosPersonasIdentity` (su `Down` borra los cuatro permisos de personas).
