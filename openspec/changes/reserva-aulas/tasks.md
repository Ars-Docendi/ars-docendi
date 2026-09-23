## 1. Base de datos y permisos

- [x] 1.1 Crear `database/aulas/001_aulas_solicitudes.sql` con la tabla
      `aulas.solicitudes_reserva` (`id`, `docente_id`, `dia`, `horario_desde`, `horario_hasta`,
      `cantidad_alumnos_aprox`, `materia`, `comision`, `estado`, `aula_asignada`, `created_at`),
      `CHECK (horario_hasta > horario_desde)`, `CHECK (cantidad_alumnos_aprox > 0)` y
      `CHECK` de estado en (`pendiente`, `aprobada`, `cancelada`).
- [x] 1.2 Agregar la migración EF en `Modules.Aulas/Infrastructure/Migrations/` que invoca el `.sql`
      embebido vía `RecursosSql` (patrón `ExcludeFromMigrations`, igual que Designaciones).
- [x] 1.3 Agregar la fila del permiso `aulas.solicitar` en un nuevo `database/identity/012_*.sql`
      (siguiente número disponible después de `011_identity_permisos_pantallas.sql`) — no editar
      archivos ya numerados existentes.
- [x] 1.4 Actualizar `database/identity/008_identity_rol_permisos.sql` (o su migración equivalente) para
      agregar `aulas.ver` + `aulas.solicitar` a `docente` y `jefe_catedra`.
- [x] 1.5 Completar `ArsDocendi.Shared/Auth/Permisos.cs`: agregar `AulasVer`, `AulasSolicitar`,
      `AulasGestionar`, `AulasAprobar` y sumarlos a `Permisos.Todos`.
- [x] 1.6 Prueba de integración: verificar que `GET /api/administracion/permisos` incluye los cuatro
      permisos de Aulas y que `docente`/`jefe_catedra` quedan con `aulas.ver` + `aulas.solicitar`
      asignados (mismo patrón que la verificación de `docentes.ver` en el change archivado de
      navegación por permisos).

## 2. Backend — dominio y persistencia

- [x] 2.1 Definir la entidad `SolicitudReservaAula` y el enum/value object de estado
      (`Pendiente`/`Aprobada`/`Cancelada`) en `Modules.Aulas/Domain/`.
- [x] 2.2 Mapear la entidad en `AulasDbContext` (`.ToTable(..., Schema, t => t.ExcludeFromMigrations())`).
- [x] 2.3 Implementar `RepositorioSolicitudesAula` en `Modules.Aulas/Repositories/`: crear, obtener por
      id, listar por `docente_id`, listar todas, actualizar estado/aula asignada.
- [x] 2.4 Implementar `ServicioSolicitudesAula` en `Modules.Aulas/Services/` con las reglas: crear
      (validar horario y cantidad de alumnos, asignar `docente_id` desde el usuario autenticado),
      cancelar (solo dueño, solo si `Pendiente`), listar propias, listar todas, asignar aula (solo si
      `Pendiente`, transiciona a `Aprobada`).
- [x] 2.5 Resolver el nombre/legajo del Docente solicitante para la vista "Todas las solicitudes"
      consultando `IConsultasIdentity.ObtenerPersonaAsync` (sin duplicar datos de persona en el schema
      `aulas`, sin agregar dependencia hacia `Modules.Portal.Contracts` — ver design.md decisión 4).

## 3. Backend — API pública

- [x] 3.1 Definir DTOs de request/response en `Modules.Aulas/Api/ModelosSolicitudes.cs` (español,
      sin exponer detalles internos de persistencia).
- [x] 3.2 `POST /api/aulas/solicitudes` — crear solicitud, `[Authorize(Policy = Permisos.AulasSolicitar)]`.
- [x] 3.3 `GET /api/aulas/solicitudes/mias` — listar propias, `[Authorize(Policy = Permisos.AulasSolicitar)]`.
- [x] 3.4 `POST /api/aulas/solicitudes/{id}/cancelar` — cancelar propia,
      `[Authorize(Policy = Permisos.AulasSolicitar)]`, rechaza si no es dueño o no está `Pendiente`.
- [x] 3.5 `GET /api/aulas/solicitudes` — listar todas, `[Authorize(Policy = Permisos.AulasAprobar)]`.
- [x] 3.6 `POST /api/aulas/solicitudes/{id}/asignar` — asignar aula, `[Authorize(Policy = Permisos.AulasAprobar)]`,
      rechaza si no está `Pendiente`.
- [x] 3.7 `IAulasQueries` queda como placeholder: ningún otro módulo necesita consultar solicitudes de
      Aulas en esta ronda; inventar un caso de uso hubiera sido especular sin consumidor real.
- [x] 3.8 Tests de integración por cada endpoint cubriendo los escenarios de
      `openspec/changes/reserva-aulas/specs/reserva-aulas/spec.md` (autorización, validaciones,
      transiciones de estado, ámbito propio vs. todo).

## 4. Frontend — datos y estado

- [x] 4.1 Definir tipos en `frontend/src/features/aulas/types.ts` (reemplaza el `export {}` stub):
      `SolicitudReservaAula`, `EstadoSolicitudAula`, DTOs de creación/asignación.
- [x] 4.2 Cliente API en `frontend/src/features/aulas/api/` usando el axios instance compartido.
- [x] 4.3 Hooks de React Query en `frontend/src/features/aulas/hooks/`: `useMisSolicitudes`,
      `useTodasLasSolicitudes`, `useCrearSolicitud`, `useCancelarSolicitud`, `useAsignarAula`, con
      invalidación de cache tras cada mutación.

## 5. Frontend — UI

- [x] 5.1 Reemplazar `frontend/src/features/aulas/pages/IndexPage.tsx`: renderiza la vista "Mis
      solicitudes" si la sesión tiene `aulas.solicitar` y/o "Todas las solicitudes" si tiene
      `aulas.aprobar` (permiso de sesión, no nombre de rol — patrón `navegacion-por-permisos`). El
      guard de ruta en `routes.tsx` necesitó extender `RequirePermission` para aceptar un array de
      permisos con semántica OR (antes solo un permiso); cambio aditivo y retrocompatible, sin romper
      los usos existentes de un único permiso.
- [x] 5.2 Componente de tabla para "Mis solicitudes" (columnas Día, Horario, Alumnos aprox., Materia,
      Comisión, Estado, Aula asignada, Acciones) reusando `shared/ui/FiltroEncabezado.tsx` para
      orden/filtro por encabezado, patrón `TablaMisPedidos`.
- [x] 5.3 Componente de tabla para "Todas las solicitudes" (mismas columnas + Docente, acción Asignar
      aula en vez de Cancelar).
- [x] 5.4 Formulario/modal "Nueva solicitud" (día, horario desde, horario hasta, cantidad aproximada de
      alumnos, materia, comisión) con validación anticipada en frontend (backend como autoridad).
- [x] 5.5 Modal de confirmación "Cancelar solicitud" (solo visible/habilitado si `Pendiente`).
- [x] 5.6 Modal "Asignar aula" para Administrativo (input de aula, solo visible/habilitado si
      `Pendiente`).
- [x] 5.7 Estados explícitos Loading, Error, Empty y Success en ambas vistas.

## 6. Documentación (mismo PR, invariantes #6 y #12)

- [x] 6.1 Completar `docs/architecture/domains/aulas.md`: entidad `SolicitudReservaAula`, los cuatro
      permisos, endpoints HTTP reales (reemplazar las filas "a definir"/"a documentar").
- [x] 6.2 Actualizar `docs/architecture/data-model.md` con el schema `aulas` y su primera tabla.
- [x] 6.3 Actualizar `docs/architecture/api-contracts.md` con los nuevos endpoints (no se creó un
      archivo específico `api-contracts-aulas.md`: la superficie de Aulas es chica, entra cómoda en la
      tabla general, a diferencia de Designaciones que tiene su propio archivo por volumen).
- [x] 6.4 Crear `docs/product/designs/reserva-aulas-design-spec.md` con el flujo de UX de ambas vistas.

## 7. Cierre

- [x] 7.1 No hay `BR-aulas-*` nuevos en esta ronda (ninguna regla proviene de normativa institucional
      identificada) — `pnpm generate-indexes` no aplica, no se corrió.
- [x] 7.2 `openspec validate reserva-aulas --strict` → válido.
- [x] 7.3 Validación end-to-end con Docker + Postgres local (`docker compose up`, `--migrate`, seed
      sintético, `dotnet test` completo, backend + frontend dev levantados y probados por HTTP real).
      Encontró y corrigió dos bugs reales que ningún test unitario/mockeado hubiera detectado: - `database/aulas/001_aulas_solicitudes.sql` no tenía `CREATE SCHEMA IF NOT EXISTS aulas;` — la
      migración fallaba en una base limpia con "schema aulas does not exist". - `PostgresFixture.CrearBaseMigradaAsync` (fixture compartido de tests de integración) nunca
      migraba `AulasDbContext` — solo Identity/Portal/Designaciones. Cualquier test de Aulas contra
      una base de tests fresca fallaba con "relation aulas.solicitudes_reserva does not exist".
      También se actualizó `PortalHttpTests.Todos_los_endpoints_ejecutan_el_crud_completo` (canario que
      cuenta el total de operaciones HTTP vía swagger.json): 66 → 71, reflejando los 5 endpoints nuevos
      de Aulas. `dotnet test ArsDocendi.slnx` completo: 154/156 verdes; los 2 restantes
      (`SeedSinteticoTests`) fallan por falta de WSL/bash en esta máquina, sin relación con este change.

## 8. Ajuste post-revisión: "Todas las solicitudes" sin columna Acciones

Pedido del usuario en revisión (ver design.md decisión 8): la columna Acciones con botón deshabilitado
para `Aprobada`/`Cancelada` no quedaba bien. Se reemplaza por doble click en la fila.

- [x] 8.1 Backend: relajar `ServicioSolicitudesAula.AsignarAulaAsync` para aceptar también estado
      `Aprobada` (actualiza `AulaAsignada` sin cambiar el estado); rechazar solo `Cancelada`.
- [x] 8.2 Actualizar `specs/reserva-aulas/spec.md`: requirement "Asignar y actualizar el aula de una
      solicitud" con el escenario de actualización sobre `Aprobada`.
- [x] 8.3 Test de integración: actualizar el aula de una solicitud `Aprobada` (permanece `Aprobada`,
      cambia el aula) y confirmar que `Cancelada` sigue rechazada.
- [x] 8.4 Frontend: `TablaTodasLasSolicitudes.tsx` — quitar columna Acciones; doble click en la fila
      abre "Asignar aula" (`Pendiente`), "Actualizar aula asignada" (`Aprobada`, precargado) o nada
      (`Cancelada`).
- [x] 8.5 `ModalAsignarAula.tsx` — modo `asignar` | `actualizar` (título, texto de botón, valor inicial
      del input), un solo componente.
- [x] 8.6 `IndexPage.tsx` (`SeccionTodasLasSolicitudes`) — estado de edición único con su modo, en vez
      de un estado separado por acción.
- [x] 8.7 `docs/product/designs/reserva-aulas-design-spec.md` y `docs/architecture/domains/aulas.md`:
      reflejar el nuevo flujo (doble click, actualizar aula sobre Aprobada).

## 9. Ajuste post-revisión: Materia como desplegable acotado (no texto libre)

Pedido del usuario en revisión (ver design.md decisión 3, actualizada): la Materia de una solicitud
debe elegirse entre las materias que el docente solicitante tiene asignadas, no texto libre.

- [x] 9.1 DDL: `database/aulas/001_aulas_solicitudes.sql` — columna `materia` (TEXT) → `materia_id`
      (UUID, FK a `identity.materias(id) ON DELETE RESTRICT`) + índice. Se edita el archivo 001 in
      place (no una migración nueva): el schema `aulas` todavía no se desplegó a ningún ambiente real.
- [x] 9.2 Dominio: `SolicitudReservaAula.Materia` (string) → `MateriaId` (Guid). `AulasDbContext` y las
      migraciones EF (Designer + Snapshot) actualizados a juego.
- [x] 9.3 Backend: `ServicioSolicitudesAula` resuelve las materias propias del actor vía
      `IConsultasIdentity.ObtenerMateriasDeRolAsync(usuarioId, rolActivo, ct)` (rol activo de la
      sesión, no fijo a `jefe_catedra` como en Designaciones — ver design.md decisión 3) y valida que
      el `materiaId` recibido en `CrearAsync` esté en ese conjunto; rechaza si no. `ListarMiasAsync`,
      `ListarTodasAsync` y `AsignarAulaAsync` resuelven el nombre/código de la materia desde
      `identity.materias` para la respuesta.
- [x] 9.4 Nuevo endpoint `GET /api/aulas/solicitudes/materias-propias` (policy `aulas.solicitar`) para
      poblar el desplegable del frontend.
- [x] 9.5 `specs/reserva-aulas/spec.md`: requirement "Crear solicitud" actualizado (materia acotada,
      escenario de rechazo por materia ajena) + nuevo requirement "Listar las materias propias del
      docente".
- [x] 9.6 Tests de integración: nuevo test de materias propias + rechazo de materia ajena; tests
      existentes actualizados para enviar `materiaId` en vez de `materia`.
- [x] 9.7 Frontend: `types.ts` (`MateriaOpcion`, `SolicitudReservaAula.materia` como objeto,
      `DatosNuevaSolicitud.materiaId`), `solicitudesApi.ts` (`listarMateriasPropias` + mapeo),
      `useMateriasPropias` hook, `ModalNuevaSolicitud.tsx` (Select en vez de Input, deshabilitado sin
      materias), tablas y `filtrosSolicitudes.ts` actualizados a `materia.nombre`.
- [x] 9.8 `PortalHttpTests.Todos_los_endpoints_ejecutan_el_crud_completo` (canario de conteo de
      endpoints vía swagger.json): 71 → 72 por el nuevo `GET /solicitudes/materias-propias`.
      `dotnet test ArsDocendi.slnx` completo: 156/158 verdes (los 2 restantes, `SeedSinteticoTests`,
      siguen sin relación — WSL/bash no disponible en esta máquina).

## 10. Ajuste post-revisión: Estado "Rechazada" con motivo, Cód. Materia y renombres de grilla

Pedido del usuario en revisión (ver design.md decisiones 9 y 10): Administrativo necesita poder
rechazar una solicitud `Pendiente` con un motivo obligatorio; el Docente dueño necesita poder ver ese
motivo; y ambas grillas deben mostrar el código de materia y renombrar dos encabezados.

- [x] 10.1 DDL: `database/aulas/001_aulas_solicitudes.sql` — agregar `'rechazada'` al CHECK
      `solicitudes_reserva_estado_valido`; agregar columna `motivo_rechazo TEXT NULL` +
      `CONSTRAINT solicitudes_reserva_motivo_si_rechazada CHECK ((estado = 'rechazada' AND
  motivo_rechazo IS NOT NULL) OR (estado <> 'rechazada' AND motivo_rechazo IS NULL))`. Se edita el
      archivo 001 in place (mismo criterio que decisión 9.1: el schema `aulas` todavía no se desplegó a
      ningún ambiente real).
- [x] 10.2 Dominio: `EstadosSolicitudAula.Rechazada = "rechazada"`; `SolicitudReservaAula.MotivoRechazo`
      (`string?`). `AulasDbContext` mapea la nueva columna.
- [x] 10.3 Backend: `IServicioSolicitudesAula.RechazarAsync(Guid id, string motivo, CancellationToken
  ct)` — valida motivo no vacío, rechaza con Conflicto si la solicitud no está `Pendiente`,
      transiciona a `Rechazada` y persiste el motivo. `AsignarAulaAsync` pasa a rechazar también sobre
      `Rechazada` (además de `Cancelada`).
- [x] 10.4 API: `RechazarSolicitudDto(string Motivo)`; `POST /api/aulas/solicitudes/{id}/rechazar`,
      `[Authorize(Policy = Permisos.AulasAprobar)]`. `SolicitudReservaAulaDto` suma `MotivoRechazo`.
- [x] 10.5 `specs/reserva-aulas/spec.md`: nuevo requirement "Rechazar solicitud pendiente
      (Administrativo)" y "Ver el motivo de una solicitud rechazada (Docente)"; actualizar "Listar mis
      solicitudes", "Listar todas las solicitudes", "Asignar y actualizar el aula de una solicitud" y
      "Orden y filtro por encabezado" para el estado `Rechazada` y el código de materia — hecho en este
      mismo commit de planning.
- [x] 10.6 Tests de integración: rechazar una `Pendiente` (motivo persistido, transición a
      `Rechazada`); rechazar sin motivo (400); rechazar una solicitud que no está `Pendiente` (409);
      asignar/actualizar aula sobre `Rechazada` (409).
- [x] 10.7 Frontend `types.ts`: `EstadoSolicitudAula` suma `"rechazada"`;
      `SolicitudReservaAula.motivoRechazo?: string`.
- [x] 10.8 Frontend `api/solicitudesApi.ts`: `rechazarSolicitud(id, motivo)`; mapear `motivoRechazo`.
- [x] 10.9 Frontend `hooks/useAccionesSolicitud.ts`: `useRechazarSolicitud`.
- [x] 10.10 `EstadoSolicitudPill.tsx`: entrada `rechazada` (tono peligro, ícono nuevo `IconoCircleX` en
      `lucide.tsx`, distinto de `IconoBan` de `cancelada`).
- [x] 10.11 `ModalAsignarAula.tsx`: botón secundario destructivo "Rechazar solicitud" en el footer,
      visible solo en modo `asignar` (no en `actualizar`); al hacer click cierra este modal y dispara
      `onRechazar(solicitud)` en vez de mutar.
- [x] 10.12 Nuevo `ModalRechazarSolicitud.tsx`: `Textarea` de motivo (obligatorio, mismo patrón que
      `ModalConfirmacionAccion`/`PanelAccionesRevision` de Designaciones), botón destructivo "Rechazar
      solicitud".
- [x] 10.13 Nuevo `ModalDetalleSolicitud.tsx`: popup de solo lectura (Día, Horario, Cód. Materia,
      Materia, Comisión, Capacidad, Estado, Motivo de rechazo) para la fila `Rechazada` de "Mis
      solicitudes".
- [x] 10.14 `TablaMisSolicitudes.tsx`: nueva columna "Cód. Materia" (antes de "Materia"); "Alumnos
      aprox." → "Capacidad"; "Aula asignada" → "Aula"; doble click en una fila `Rechazada` abre
      `ModalDetalleSolicitud` (única fila accionable por doble click en esta tabla).
- [x] 10.15 `TablaTodasLasSolicitudes.tsx`: misma columna nueva y mismos renombres; una fila
      `Rechazada` deja de considerarse "accionable" (ya no reacciona al doble click, es terminal igual
      que `Cancelada`).
- [x] 10.16 `IndexPage.tsx` (`SeccionTodasLasSolicitudes`): estado `solicitudARechazar`; conecta
      `ModalAsignarAula.onRechazar` → abre `ModalRechazarSolicitud` → `useRechazarSolicitud`.
      (`SeccionMisSolicitudes`): estado `solicitudDetalle`; conecta doble click → `ModalDetalleSolicitud`.
- [x] 10.17 `docs/product/designs/reserva-aulas-design-spec.md` y `docs/architecture/domains/aulas.md`:
      reflejar el estado `Rechazada`, el endpoint nuevo, y las columnas/renombres de grilla.
- [x] 10.18 `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend lint`,
      `pnpm --filter frontend build` y validación manual end-to-end (Docente rechaza → ve motivo;
      Administrativo rechaza con y sin motivo). Validación con Docker + Postgres local: 158/160 tests
      verdes (los 2 restantes, `SeedSinteticoTests`, siguen sin relación — WSL/bash no disponible en
      esta máquina, mismo caso preexistente de la sección 9). Encontrado y corregido en la validación
      manual: `ModalAsignarAula` y `ModalRechazarSolicitud` usaban la misma `key="cerrado"` cuando
      ambos estaban cerrados — React lo reportaba como children con key duplicada entre hermanos
      (warning en consola, sin romper la UI); se distinguieron a `"asignar-cerrado"` /
      `"rechazar-cerrado"`.
