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
- **No pertenece**: Datos personales del docente (viven en `identity.personas`), asignaciones académicas reales en Guaraní (se consultan, no se persisten acá).

## Entidades principales

| Entidad           | Descripción                                                                    | Schema/Tabla                     |
| ----------------- | ------------------------------------------------------------------------------ | -------------------------------- |
| `Cargo`           | Catálogo único de cargos docentes. `orden` registra la jerarquía institucional | `designaciones.cargos`           |
| `Periodo`         | Ventana de carga + rango de impacto. A lo sumo uno activo a la vez             | `designaciones.periodos`         |
| `Pedido`          | **El trámite.** Cubre exactamente una materia — la cátedra del Jefe de Cátedra | `designaciones.pedidos`          |
| `PedidoAdjunto`   | Documentación respaldatoria (CV, DNI, justificativo)                           | `designaciones.pedido_adjuntos`  |
| `PedidoHistorial` | Línea de tiempo del trámite, con el rol con el que se actuó y el comentario    | `designaciones.pedido_historial` |
| `Designacion`     | **El estado vigente** `(persona, materia, cargo, horas)` con vigencia          | `designaciones.designaciones`    |

### Pedido vs Designación

Son las dos entidades centrales y quieren propiedades opuestas. Confundirlas es el error de modelado que este dominio evita:

|                        | Pedido (trámite)                   | Designación (estado)    |
| ---------------------- | ---------------------------------- | ----------------------- |
| Naturaleza             | Inmutable una vez enviado          | Mutable, tiene vigencia |
| Verdad que sostiene    | "Esto decía cuando se firmó"       | "Esto es cierto hoy"    |
| Vínculo con la persona | FK viva **+** `snapshot` congelado | FK viva                 |
| Vida                   | Termina en un estado terminal      | Vive hasta una Baja     |

El `snapshot` se congela **al enviar** a revisión, no al crear. Es lo que le da valor probatorio al trámite: un pedido aprobado en Decanato tres meses después sigue diciendo qué cargo tenía el docente el día que se cargó. Si se recalculara, el documento reescribiría su propio pasado.

### Trazabilidad

Aprobar un pedido se traduce a escrituras sobre `designaciones` en una única transacción:

```
Alta        → INSERT de una designación nueva
Baja        → UPDATE de vigente_hasta sobre la vigente
Cambio      → cierra la vigente y abre una nueva con lo solicitado
Sin novedad → no toca nada
```

`origen_pedido_id` en NULL significa **carga administrativa directa**: la pantalla de administración de docentes escribe esta misma tabla. Son dos caminos de escritura hacia una sola tabla, y esa columna es lo único que permite distinguirlos.

La cadena completa hacia atrás: designación vigente → `origen_pedido_id` → pedido → `pedido_historial` → `audit.change_log`.

### Un pedido, una materia

`identity.roles` define `jefe_catedra` con `scope = 'materia'`: cátedra **es** materia. Por eso el pedido lleva una sola `materia_id`, y la carrera se deriva de `identity.materias.carrera_id` en vez de desnormalizarse. Con una lista de N materias, un pedido podía abarcar dos carreras y dejar a dos Coordinadores compitiendo por él, sin que BR-designaciones-009 tuviera cómo resolverlo.

## API pública (contract)

| Interfaz                       | Métodos                                             | Consumido por                      |
| ------------------------------ | --------------------------------------------------- | ---------------------------------- |
| `IAdministracionDesignaciones` | listar, validar y reemplazar designaciones vigentes | superficie administrativa del Host |

El contract transporta UUIDs y DTOs puros de asignación; no expone entidades EF, repositorios ni el `DesignacionesDbContext`. La administración de docentes puede coordinar persona, rol docente y designaciones sin adquirir una referencia al módulo interno.

## Endpoints HTTP

| Recurso            | Path                                       | Autoridad                                           |
| ------------------ | ------------------------------------------ | --------------------------------------------------- |
| Períodos           | `/api/designaciones/periodos`              | permiso `periodos.administrar`                      |
| Catálogos acotados | `/api/designaciones/catalogos`             | identidad y ámbitos persistidos                     |
| Pedidos y detalle  | `/api/designaciones/pedidos[/{id}]`        | permisos y visibilidad resueltos por backend        |
| Transiciones       | `/api/designaciones/pedidos/{id}/{accion}` | máquina de estados, permiso de etapa e idempotencia |

El contrato completo está en [api-contracts-designaciones.md](../api-contracts-designaciones.md).

## Reglas de negocio

Ver [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md).

BR-designaciones-001 es la única con implementación en la base: índice único parcial sobre `(periodo_id, persona_id)` excluyendo los estados terminales, más validación en el backend para el mensaje de error. La base es la autoridad — es lo único que sobrevive a dos requests concurrentes.

## Dependencias

- **Hacia `identity`** (vía `IConsultasIdentity`, sólo lectura): resolver la persona, validar el rol de Jefe de Cátedra sobre la materia del pedido, derivar la carrera. El módulo **no escribe** identity.
- **Hacia adentro**: `Modules.Portal.Contracts` (consultar áreas de experticia — proyectado, no confirmado).
- **Hacia afuera**: ninguna por ahora.
- **Externas**: **API Guaraní** (lectura de asignaciones existentes — detalle de integración TBD).

## Specs activas

_(autogenerable a futuro)_

## Decisiones registradas

- **Solo lectura de Guaraní**: las asignaciones reales viven en Guaraní; Ars Docendi solo las consulta para mostrarlas en contexto. NO replicar esa data en nuestra DB.
