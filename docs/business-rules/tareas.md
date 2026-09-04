# Business rules: `tareas`

## Contexto

- **Módulo / superficie:** `frontend/src/features/tareas/` (frontend-first; sin `Modules.Tareas` backend todavía, más allá del `GET /api/tareas/ping`).
- **Owner / stakeholders:** Secretaría Académica, Decanato, Administrativos (autoridades que crean y gestionan tareas).
- **Change/Spec OpenSpec relacionado:** `openspec/changes/sistema-tareas/` → `openspec/specs/{tareas,tablero-tareas,flujo-estado-tareas}/`.
- **Normativa de referencia:** ninguna. El módulo Tareas es coordinación administrativa interna del departamento, no un trámite regulado por el estatuto o el régimen académico.

## Reglas

Sin `BR-tareas-NNN` en esta sección. Ninguna regla de este change proviene de normativa institucional citable — son decisiones de producto (permisos de estado, semáforo, campos obligatorios), documentadas como requisitos en `openspec/specs/flujo-estado-tareas/spec.md` y `openspec/specs/tablero-tareas/spec.md`, no como `BR-*`. Si en el futuro una regla de Tareas naciera de una normativa (ej. un plazo mínimo de resolución fijado por reglamento interno), se agrega acá con su cita exacta en ese momento.

## Mapping a tests

_(no aplica — sin `BR-*` en este módulo. La cobertura de los requisitos de producto vive en `frontend/src/features/tareas/api/maquinaEstadosTarea.test.ts` y `frontend/src/features/tareas/components/semaforoTarea.test.ts`.)_

## Assumptions (a confirmar)

- (ninguna pendiente)

## Open Questions

- (ninguna pendiente)

## Aprobación

_(no aplica — sin normativa institucional que aprobar en este módulo.)_
