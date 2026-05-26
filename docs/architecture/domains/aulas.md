# Domain: Aulas

## Propósito

Gestión de **pedidos y asignación de aulas y laboratorios** para mesas de examen (no para uso académico regular, que ya está coordinado por otra vía).

## Roles que interactúan

- **Docente / Jefe de Cátedra** — Solicita aula para examen.
- **Administrativos** — Gestionan las reservas, asignan aulas, resuelven conflictos.
- **Secretaría Académica** — Configurables del módulo (qué aulas existen, capacidades, equipamiento).

## Bounded context

- **Pertenece**: Aulas, laboratorios, pedidos de reserva, asignaciones, períodos de exámenes.
- **No pertenece**: Usuarios del aula (vienen del Portal), información académica de las materias.

## Entidades principales

| Entidad                                  | Descripción | Schema/Tabla |
| ---------------------------------------- | ----------- | ------------ |
| _(a definir en spec inicial del módulo)_ | ...         | `aulas.*`    |

## API pública (contract)

| Interfaz      | Métodos | Consumido por |
| ------------- | ------- | ------------- |
| _(a definir)_ | ...     | ...           |

## Endpoints HTTP

| Método                    | Path              | Rol       | Descripción  |
| ------------------------- | ----------------- | --------- | ------------ |
| GET                       | `/api/aulas/ping` | (anónimo) | Health check |
| _(a documentar en specs)_ | ...               | ...       | ...          |

## Reglas de negocio

Ver [`docs/business-rules/aulas.md`](../../business-rules/aulas.md) (a crear).

## Dependencias

- **Hacia adentro**: `Modules.Portal.Contracts` (conocer al docente solicitante).
- **Hacia afuera**: ninguna por ahora.
- **Externas**: ninguna conocida.

## Specs activas

_(autogenerable a futuro)_

## Decisiones registradas

- **Solo exámenes**: el alcance no incluye aulas para clases regulares — ese flujo existe institucionalmente por otra vía y no se va a duplicar.
