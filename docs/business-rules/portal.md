# Business rules: `portal`

## Contexto

- **Módulo / superficie:** `backend/src/Modules.Portal/` como autoridad; `frontend/src/features/portal/` y `frontend/src/shared/perfil/` como clientes.
- **Owner / stakeholders:** Secretaría Académica (cura el vocabulario); Docentes (mantienen su perfil).
- **Specs OpenSpec relacionadas:** [`portal-docente-api`](../../openspec/specs/portal-docente-api/spec.md), [`experticia-docente`](../../openspec/specs/experticia-docente/spec.md) y el change `catalogo-aulas-y-vocabulario-portal`.
- **Normativa de referencia:** ninguna citada. Las reglas son decisiones de producto y de proceso.

## Reglas

### BR-`portal`-001 Cada docente edita sólo su perfil

- **Statement:** `/api/portal/perfil` opera siempre sobre la persona autenticada; la identidad institucional (nombre, documento, legajo) es de solo lectura.
- **Rationale:** El perfil es autogestión; la identidad la administra quien da de alta personas.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de producto, no normativa.
- **Ejemplos:** Ningún endpoint de Mi Portal recibe el id de otra persona.
- **Roles afectados:** Docente.

### BR-`portal`-002 Un término no se repite por normalización

- **Statement:** El vocabulario y cada lista de un perfil no contienen dos términos iguales sin distinguir mayúsculas ni espacios extremos.
- **Rationale:** "C#" y " c# " son la misma habilidad; duplicarla ensucia las búsquedas.
- **Provenance:** `from_spec`
- **Fuente normativa:** Decisión de producto, no normativa.
- **Ejemplos:** Guardar "C#" y " c# " deja un solo tag; renombrar un término a uno que ya existe devuelve 409 `termino-repetido`.
- **Roles afectados:** Docente, Secretaría Académica.

### BR-`portal`-003 Un término nuevo queda sugerido hasta incorporarse

- **Statement:** Un término que el docente agrega fuera del vocabulario queda en su perfil marcado como sugerido y no se ofrece a otros hasta que la Secretaría lo incorpora.
- **Rationale:** El vocabulario crece desde el uso, pero lo curan quienes parametrizan el departamento.
- **Provenance:** `confirmed_user`
- **Fuente normativa:** Decisión de producto, no normativa.
- **Ejemplos:** "Docencia virtual" aparece con la marca "Sugerido"; al incorporarlo, figura en el selector de todos.
- **Roles afectados:** Docente, Secretaría Académica.

### BR-`portal`-004 Una variante fusionada resuelve a su término canónico

- **Statement:** Fusionar A en B pasa todos los usos de A a B sin duplicar; desde entonces, guardar A registra B.
- **Rationale:** Unifica variantes ("BD", "Bases de datos") sin pedirle a cada docente que edite su perfil.
- **Provenance:** `confirmed_user`
- **Fuente normativa:** Decisión de producto, no normativa.
- **Ejemplos:** Un docente con "BD" y "Bases de datos" queda con "Bases de datos" una sola vez; un editor abierto antes de la fusión recibe 409.
- **Roles afectados:** Docente, Secretaría Académica.

### BR-`portal`-005 Editar el perfil de otra persona requiere permiso y ámbito

- **Statement:** El perfil de otra persona se lee con `personas.ver` (o `personas.ver_ambito` dentro de su ámbito) y se edita además con `portal.administrar`.
- **Rationale:** Corregir un perfil ajeno es una tarea administrativa acotada al ámbito de quien la hace.
- **Provenance:** `inferred_from_code`
- **Fuente normativa:** Decisión de proceso, no normativa.
- **Ejemplos:** Un Jefe de Cátedra fuera de su ámbito recibe 404.
- **Roles afectados:** Secretaría Académica, Administrativos, Decanato, Jefe de Cátedra.

## Mapping a tests

| Rule ID       | Test file(s)                                                                                                                                                                                                            | Tipo                     | Notas                                     |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------ | ----------------------------------------- |
| BR-portal-001 | `backend/tests/ArsDocendi.IntegrationTests/Portal/PortalHttpTests.cs::Todos_los_endpoints_ejecutan_el_crud_completo`; `frontend/src/features/portal/pages/miPortal.test.tsx` → bloque Perfil                            | integración + componente | La identidad no tiene endpoint de edición |
| BR-portal-002 | `Portal/AltasConcurrentesTests.cs::Dos_perfiles_sugieren_un_termino_normalizado_y_conservan_tipos_independientes`; `Portal/MiPortalHttpTests.cs::Incorporar_ofrece_el_termino_y_renombrar_a_uno_existente_es_conflicto` | integración PostgreSQL   |                                           |
| BR-portal-003 | `Portal/MiPortalHttpTests.cs::Incorporar_ofrece_el_termino_y_renombrar_a_uno_existente_es_conflicto`; `miPortal.test.tsx` → vocabulario                                                                                 | integración + componente |                                           |
| BR-portal-004 | `Portal/MiPortalHttpTests.cs::Fusionar_mueve_los_usos_sin_duplicar_invalida_editores_y_resuelve_guardados_futuros`                                                                                                      | integración PostgreSQL   |                                           |
| BR-portal-005 | `backend/tests/ArsDocendi.IntegrationTests/Backend/PersonasHttpTests.cs::Portal_de_otra_persona_se_corrige_con_permiso_y_ambito`                                                                                        | integración PostgreSQL   |                                           |

Todo BR-\* debe tener al menos un test verificando la regla.

## Assumptions (a confirmar)

- La curaduría del vocabulario corresponde a Secretaría Académica (y `sys_admin`).

## Open Questions

- Ninguna.

## Aprobación

- **Aprobado por:** pendiente
- **Fecha:** pendiente
- **Versión de la normativa vigente al aprobar:** no aplica (reglas de producto y proceso)
