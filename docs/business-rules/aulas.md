# Business rules: `aulas`

## Contexto

- **Módulo / superficie:** `backend/src/Modules.Aulas/` como autoridad y `frontend/src/features/aulas/` como cliente de su API.
- **Owner / stakeholders:** Administrativos y Secretaría Académica (asignan y administran el catálogo); Docentes y Jefes de Cátedra (solicitan).
- **Specs OpenSpec relacionadas:** [`reserva-aulas`](../../openspec/specs/reserva-aulas/spec.md), [`detalle-solicitud-reserva-aula`](../../openspec/specs/detalle-solicitud-reserva-aula/spec.md) y el change `catalogo-aulas-y-vocabulario-portal`.
- **Normativa de referencia:** ninguna citada. Las reglas son decisiones de proceso del departamento; si alguna resulta tener origen normativo, la cita queda **pendiente de confirmación con el cliente**.

## Reglas

### BR-`aulas`-001 Se pide aula sólo para materias propias

- **Statement:** Un docente sólo puede solicitar aula para una materia que tiene asignada en su rol activo.
- **Rationale:** Evita reservas a nombre de cátedras ajenas.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** Con "Ingeniería de Software" asignada, la solicitud se crea; con una materia que no tiene, la API responde validación.
- **Roles afectados:** Docente, Jefe de Cátedra.

### BR-`aulas`-002 Horario y cantidad de alumnos válidos

- **Statement:** El horario hasta debe ser posterior al horario desde y la cantidad aproximada de alumnos mayor a cero.
- **Rationale:** Una reserva sin duración o sin alumnos no se puede asignar.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** 10:00–09:00 o 0 alumnos se rechazan. La base lo garantiza con `solicitudes_reserva_horario_valido` y `solicitudes_reserva_alumnos_valido`.
- **Roles afectados:** Docente, Jefe de Cátedra.

### BR-`aulas`-003 Rechazar o cancelar una reserva exige motivo

- **Statement:** Rechazar una solicitud pendiente y cancelar una reserva aprobada exigen un motivo no vacío, que ve el docente.
- **Rationale:** El docente necesita saber por qué se quedó sin aula para reorganizar el examen.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** Un motivo en blanco devuelve validación sobre `motivo`; cancelar una solicitud que no está aprobada devuelve 409.
- **Roles afectados:** Administrativo, Secretaría Académica.

### BR-`aulas`-004 El docente sólo cancela sus solicitudes pendientes

- **Statement:** El docente puede cancelar una solicitud propia mientras está pendiente; una ajena o ya resuelta no.
- **Rationale:** Una vez aprobada, la cancelación la decide quien administra las aulas.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** Cancelar una ajena devuelve 403 `solicitud-ajena`; cancelar dos veces devuelve 409.
- **Roles afectados:** Docente, Jefe de Cátedra.

### BR-`aulas`-005 Un aula no tiene dos reservas aprobadas superpuestas

- **Statement:** Una misma aula no puede tener dos reservas aprobadas el mismo día con horarios superpuestos. Los horarios son intervalos semiabiertos: una reserva que termina 10:00 no choca con otra que empieza 10:00.
- **Rationale:** Es el error que el texto libre permitía: dos mesas en la misma aula al mismo tiempo.
- **Provenance:** `confirmed_user`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** Aula 204 aprobada 9:00–11:00; asignarla 10:00–12:00 devuelve 409 `aula-ocupada`; 11:00–13:00 se aprueba.
- **Roles afectados:** Administrativo, Secretaría Académica.
- **Implementación:** la autoridad es el `EXCLUDE` `solicitudes_reserva_aula_sin_superposicion` (también bajo asignaciones concurrentes); el servicio verifica antes sólo para dar el mensaje.

### BR-`aulas`-006 Sólo se asignan aulas activas del catálogo

- **Statement:** El aula asignada debe existir en el catálogo y estar activa. Un aula con reservas no se elimina: se desactiva.
- **Rationale:** El catálogo es la fuente única de aulas; desactivar conserva el historial de las reservas.
- **Provenance:** `confirmed_user`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** Asignar un aula desactivada devuelve validación sobre `aulaId`; eliminar un aula con reservas devuelve 409 `aula-en-uso`.
- **Roles afectados:** Administrativo, Secretaría Académica.

### BR-`aulas`-007 Superar la capacidad advierte, no bloquea

- **Statement:** Si la cantidad aproximada de alumnos supera la capacidad del aula, la interfaz lo advierte y la asignación se permite igual.
- **Rationale:** La cantidad es una estimación del docente; quien asigna decide.
- **Provenance:** `confirmed_user`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** 30 alumnos en un aula de 10 muestra "Capacidad insuficiente" y se puede asignar.
- **Roles afectados:** Administrativo, Secretaría Académica.

## Mapping a tests

| Rule ID      | Test file(s)                                                                                                                                                                         | Tipo                       | Notas |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------- | ----- |
| BR-aulas-001 | `backend/tests/ArsDocendi.IntegrationTests/Aulas/AulasHttpTests.cs::Lista_las_materias_propias_y_rechaza_una_solicitud_con_materia_ajena`                                            | integración PostgreSQL     |       |
| BR-aulas-002 | `AulasHttpTests.cs::Rechaza_horario_invalido_y_cantidad_de_alumnos_invalida`; `frontend/src/features/aulas/components/validacionNuevaSolicitud.test.ts`                              | integración + unit         |       |
| BR-aulas-003 | `AulasHttpTests.cs::No_se_puede_rechazar_sin_motivo_ni_una_solicitud_que_no_este_pendiente`, `::No_se_cancela_una_reserva_sin_motivo_ni_fuera_de_aprobada_ni_sin_permiso`            | integración PostgreSQL     |       |
| BR-aulas-004 | `AulasHttpTests.cs::No_se_puede_cancelar_dos_veces_ni_una_solicitud_ajena`                                                                                                           | integración PostgreSQL     |       |
| BR-aulas-005 | `AulasHttpTests.cs::Un_aula_no_se_asigna_a_dos_reservas_superpuestas_pero_si_a_horarios_contiguos`, `::Dos_asignaciones_concurrentes_del_mismo_aula_dejan_una_sola_reserva_aprobada` | integración + concurrencia |       |
| BR-aulas-006 | `AulasHttpTests.cs::Se_asigna_solo_un_aula_activa_aunque_la_capacidad_quede_corta`, `::El_catalogo_exige_aulas_gestionar_nombres_unicos_y_no_borra_un_aula_en_uso`                   | integración PostgreSQL     |       |
| BR-aulas-007 | `AulasHttpTests.cs::Se_asigna_solo_un_aula_activa_aunque_la_capacidad_quede_corta`; `frontend/src/features/aulas/pages/DetalleSolicitudPage.test.tsx` → advertencia de capacidad     | integración + componente   |       |

Todo BR-\* debe tener al menos un test verificando la regla.

## Assumptions (a confirmar)

- Las comisiones siguen como texto libre hasta la integración con Guaraní.
- No hay períodos de exámenes: se puede pedir aula para cualquier día.

## Open Questions

- Ninguna.

## Aprobación

- **Aprobado por:** pendiente
- **Fecha:** pendiente
- **Versión de la normativa vigente al aprobar:** no aplica (reglas de proceso)
