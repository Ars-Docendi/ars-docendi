## Why

El módulo Aulas es hoy un skeleton: expone únicamente `GET /api/aulas/ping` y la pantalla `/aulas` del
frontend muestra el stub "Módulo en construcción — RF-02 Reserva de Aulas / Laboratorios." Los docentes
no tienen forma digital de pedir un aula para una mesa de examen, y Administrativos no tienen forma de
asignarla — el proceso hoy es manual, fuera del sistema. Esta es la primera vez que se define el
esquema de datos y el flujo de negocio de Aulas.

## What Changes

- Nueva entidad `SolicitudReservaAula` persistida en el schema Postgres `aulas`, con DDL versionado en
  `database/aulas/` (siguiendo el patrón de `database/designaciones/`).
- Nuevos endpoints en `Modules.Aulas` (Controller → Service → Repository): crear solicitud, listar
  solicitudes propias (Docente), listar todas las solicitudes (Administrativo), cancelar solicitud
  propia (Docente) y asignar aula / aprobar solicitud (Administrativo).
- Nuevo permiso `aulas.solicitar` en el catálogo de identidad (`identity.permisos`), para separar
  "crear y cancelar solicitudes propias" (Docente) de "gestionar y aprobar" (Administrativo) — mismo
  patrón self-service vs. administración que `designaciones.gestionar` vs. `designaciones.revisar`.
- Ajuste de la matriz de permisos por rol (`database/identity/008_identity_rol_permisos.sql`, ya
  documentada en el propio archivo como PROVISIONAL/pendiente de confirmación con el cliente): se
  agrega `aulas.ver` + `aulas.solicitar` al rol `docente`, que hoy no tiene ningún permiso de Aulas a
  pesar de ser quien solicita. `jefe_catedra` recibe el mismo agregado (ya tenía `aulas.ver`, es
  también docente). No se toca la fila de `administrativo`/`secretaria` (ya tienen
  `aulas.ver` + `aulas.gestionar` + `aulas.aprobar`).
- Reemplazo del stub `frontend/src/features/aulas/pages/IndexPage.tsx` por una pantalla real
  `/aulas`, con dos vistas condicionadas por permiso (no por nombre de rol, según
  `navegacion-por-permisos`): "Mis solicitudes" (`aulas.solicitar`: lista propia + botón Nueva
  solicitud + acción Cancelar) y "Todas las solicitudes" (`aulas.aprobar`: lista completa + acción
  Asignar aula). Un usuario con ambos permisos (p. ej. `administrativo` si en el futuro also
  solicitara) vería ambas.
- Formulario de nueva solicitud: día, horario desde, horario hasta, cantidad aproximada de alumnos,
  materia y comisión.
- Acción de asignación de aula (Administrativo) que transiciona la solicitud de `Pendiente` a
  `Aprobada` y persiste el número/identificador de aula asignada.
- Tabla ordenable y filtrable por columna en ambas vistas, reusando los componentes compartidos
  `shared/ui/FiltroEncabezado.tsx` (mismo patrón que
  `TablaMisPedidos` de Designaciones).

**BREAKING**: ninguno — `Modules.Aulas.Contracts` no tiene consumidores cross-module todavía
(`IAulasQueries` es un placeholder vacío), así que definirlo no rompe a nadie.

## Capabilities

### New Capabilities

- `reserva-aulas`: ciclo de vida completo de una solicitud de reserva de aula — crear, listar propias,
  listar todas, cancelar y asignar aula/aprobar — con autorización por permiso y por ámbito (propio
  para Docente, completo para Administrativo), y la tabla ordenable/filtrable que las expone.

### Modified Capabilities

_(ninguna — `navegacion-por-permisos` ya cubre de forma genérica el mecanismo de sidebar/route guard
por permiso; este change solo agrega un permiso nuevo al catálogo de datos, no cambia ese mecanismo. No
existe ningún spec vigente que enumere o fije los permisos de Aulas o la matriz rol→permiso como
requirement, así que ajustar esa matriz no es una modificación de capability.)_

## Impact

- **Backend**: `Modules.Aulas` (entidad, `AulasDbContext`, controller, service, repository nuevos),
  `Modules.Aulas.Contracts` (`IAulasQueries` deja de ser placeholder). El docente solicitante se
  resuelve vía `ArsDocendi.Shared.Identity.IConsultasIdentity` (mismo mecanismo que usa
  `Modules.Portal` para resolverse a sí mismo) — no vía `Modules.Portal.Contracts`, así que el edge
  `Aulas → Portal.Contracts` anticipado en `dependency-graph.md` queda sin materializar y no se agrega
  ninguna dependencia cross-module nueva. Sin cambios en `ArsDocendi.Host` (módulo ya registrado).
- **Base de datos**: nuevo `database/aulas/001_aulas_solicitudes.sql` (primer DDL real del schema
  `aulas`); nueva fila de permiso en `database/identity/007_identity_permisos.sql`; ajuste de
  `database/identity/008_identity_rol_permisos.sql`.
- **Frontend**: reemplazo completo de `frontend/src/features/aulas/*` (routes, pages, nuevos
  components/hooks/api siguiendo la anatomía de `features/designaciones/`); reuso de
  `shared/ui/FiltroEncabezado.tsx`. Sin cambios en `nav.ts` (el enlace
  `/aulas` con permiso `aulas.ver` ya existe).
- **Docs** (a actualizar en el mismo PR que el código, invariante #6/#12):
  `docs/architecture/domains/aulas.md` (entidades, permisos, endpoints), `docs/architecture/data-model.md`
  (nuevo schema `aulas`), `docs/product/designs/reserva-aulas-design-spec.md` (nuevo, cambio de UX).
- **Reglas de negocio**: ninguna proviene de normativa institucional identificada hasta ahora — no se
  registra `BR-aulas-NNN` en esta ronda; si surge una restricción reglamentaria (p. ej. capacidad
  mínima de aula vs. alumnos) se agrega en la implementación con su cita.
- **Rollback**: el módulo Aulas no tiene consumidores ni datos productivos hoy — revertir es eliminar
  el DDL nuevo (`database/aulas/001_*.sql`, aún no aplicado a producción) y la fila de permiso, sin
  afectar otros módulos.
