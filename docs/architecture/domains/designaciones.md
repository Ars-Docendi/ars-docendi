# Domain: Designaciones

## Propósito

Workflow de **designaciones docentes**: solicitud por Jefe de Cátedra → aprobación/rechazo por Coordinador de Carrera → revisión por Secretaría Académica → aprobación final por Decanato. Visualización de asignaciones existentes consultando la **API Guaraní**.

## Roles que interactúan

- **Jefe de Cátedra** — Genera el proyecto docente de su cátedra (designaciones propuestas para el período).
- **Coordinador de Carrera** — Aprueba o rechaza novedades en designaciones de docentes de su carrera.
- **Secretaría Académica** — Vista global del departamento. Parametriza el sistema (períodos, cargos, etc.).
- **Decanato** — Aprobación final del proyecto docente consolidado.

## Bounded context

- **Pertenece**: Designaciones, períodos lectivos, propuestas, aprobaciones, novedades sobre designaciones.
- **No pertenece**: Datos personales del docente (vive en `Portal`), asignaciones académicas reales en Guaraní (se consultan, no se persisten acá).

## Entidades principales

| Entidad                                  | Descripción | Schema/Tabla      |
| ---------------------------------------- | ----------- | ----------------- |
| _(a definir en spec inicial del módulo)_ | ...         | `designaciones.*` |

## API pública (contract)

| Interfaz      | Métodos | Consumido por |
| ------------- | ------- | ------------- |
| _(a definir)_ | ...     | ...           |

## Endpoints HTTP

| Método                    | Path                      | Rol       | Descripción  |
| ------------------------- | ------------------------- | --------- | ------------ |
| GET                       | `/api/designaciones/ping` | (anónimo) | Health check |
| _(a documentar en specs)_ | ...                       | ...       | ...          |

## Reglas de negocio

Ver [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md) (a crear cuando se identifiquen las primeras BR-\*).

## Dependencias

- **Hacia adentro**: `Modules.Portal.Contracts` (validar que el docente designado existe en el portal, consultar áreas de experticia).
- **Hacia afuera**: ninguna por ahora.
- **Externas**: **API Guaraní** (lectura de asignaciones existentes — detalle de integración TBD).

## Specs activas

_(autogenerable a futuro)_

## Decisiones registradas

- **Solo lectura de Guaraní**: las asignaciones reales viven en Guaraní; Ars Docendi solo las consulta para mostrarlas en contexto. NO replicar esa data en nuestra DB.
