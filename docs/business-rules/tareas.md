# Business rules: `tareas`

## Contexto

- **Módulo / superficie:** backend `Modules.Tareas` (`/api/tareas`) y frontend `frontend/src/features/tareas/`; reglas de negocio validadas en servicio y persistencia SQL.
- **Owner / stakeholders:** Secretaría Académica, Decanato, Administrativos (autoridades que crean y gestionan tareas).
- **Change/Spec OpenSpec relacionado:** `openspec/changes/sistema-tareas/` → `openspec/changes/sistema-tareas/specs/{tareas,tablero-tareas,flujo-estado-tareas,proyectos}/`.
- **Normativa de referencia:** ninguna. El módulo Tareas es coordinación administrativa interna del departamento, no un trámite regulado por el estatuto o el régimen académico.

## Reglas

Sin `BR-tareas-NNN` en esta sección. Ninguna regla de este change proviene de normativa institucional citable — son decisiones de producto (permisos de estado, semáforo, campos obligatorios, la restricción de Proyectos a Decanato/Secretaría Académica, la jerarquía de asignación de Responsable), documentadas como requisitos en `openspec/changes/sistema-tareas/specs/{flujo-estado-tareas,tablero-tareas,tareas,proyectos}/spec.md`, no como `BR-*`. Si en el futuro una regla de Tareas naciera de una normativa (ej. un plazo mínimo de resolución fijado por reglamento interno), se agrega acá con su cita exacta en ese momento.

## Mapping a tests

No se agregan `BR-tareas-*`: estas son garantías técnicas/reglas de producto, no normativa institucional. La cobertura persistente está en `backend/tests/ArsDocendi.IntegrationTests/Tareas/{CierreTareasHttpTests,ConcurrenciaTareasHttpTests,IntegridadTareasHttpTests,IntegridadTareasSqlTests,TareasHttpTests}.cs` y `frontend/src/features/tareas/`; evidencia de ejecución integrada: [post-auditoría](../evidence/portal-storage-tareas-post-auditoria.md). Las suites prueban jerarquía/proyecto heredado, guards de versión y 409, relaciones idempotentes, filtro de candidatos, constraints SQL con códigos de catálogo, secuencias de numeración y auditoría sin snapshots de campos libres; el ping público se prueba por HTTP.

## Assumptions (a confirmar)

- (ninguna pendiente)

## Open Questions

- (ninguna pendiente)

## Aprobación

_(no aplica — sin normativa institucional que aprobar en este módulo.)_
