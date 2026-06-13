# Business rules: `<modulo>`

## Contexto

- **Módulo / superficie:** (path o proyecto, ej. `backend/src/Modules.Designaciones/`)
- **Owner / stakeholders:** (rol institucional responsable, ej. "Secretaría Académica")
- **Change/Spec OpenSpec relacionado:** `openspec/specs/<capability>/` o `openspec/changes/<id>/` (si aplica)
- **Normativa de referencia:** (estatuto / régimen / disposición departamental aplicable)

## Reglas

### BR-`<modulo>`-001 `<título corto>`

- **Statement:** (una oración precisa)
- **Rationale:** (por qué existe — qué problema institucional resuelve o qué normativa cumple)
- **Provenance:** `confirmed_user` | `user_edited_agent_draft` | `inferred_from_code` | `from_spec` | `from_regulation`
- **Fuente normativa:** (cita exacta del documento normativo, con artículo/sección si aplica)
- **Ejemplos:** (inputs / outcomes concretos, incluyendo casos negativos)
- **Roles afectados:** (qué roles deben respetar esta regla)

### BR-`<modulo>`-002 `<título corto>`

- **Statement:**
- **Rationale:**
- **Provenance:**
- **Fuente normativa:**
- **Ejemplos:**
- **Roles afectados:**

## Mapping a tests

| Rule ID      | Test file(s)                                   | Tipo             | Notas |
| ------------ | ---------------------------------------------- | ---------------- | ----- |
| BR-`<m>`-001 | `backend/tests/.../...Tests.cs::nombreDelTest` | unit/integration |       |

Todo BR-\* debe tener al menos un test verificando la regla.

## Assumptions (a confirmar)

- (creencias temporales no confirmadas — eliminar cuando promovidas a Rules u Open Questions)

## Open Questions

- (deben estar vacías o explícitamente diferidas con owner/fecha antes de generar tests de negocio que dependan de esos puntos)

## Aprobación

- **Aprobado por:** (nombre / rol)
- **Fecha:**
- **Versión de la normativa vigente al aprobar:** (fecha de la versión de la fuente normativa)
