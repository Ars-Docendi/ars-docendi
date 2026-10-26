# Domain: Designaciones

## Propósito

Workflow de **designaciones docentes**: solicitud por Jefe de Cátedra → aprobación/rechazo por Coordinador de Carrera → revisión por Secretaría Académica → aprobación final por Decanato.

## Roles que interactúan

- **Jefe de Cátedra** — Genera el proyecto docente de su cátedra (designaciones propuestas para el período).
- **Coordinador de Carrera** — Aprueba o rechaza novedades en designaciones de docentes de su carrera.
- **Secretaría Académica** — Vista global del departamento. Parametriza el sistema (períodos, cargos, etc.).
- **Decanato** — Aprobación final del proyecto docente consolidado.

## Bounded context

- **Pertenece**: Designaciones, períodos lectivos, propuestas, aprobaciones, novedades sobre designaciones.
- **No pertenece**: Datos personales del docente (viven en `identity.personas`), asignaciones académicas reales (viven en Guaraní; no hay integración).

## Entidades principales

| Entidad           | Descripción                                                                                 | Schema/Tabla                     |
| ----------------- | ------------------------------------------------------------------------------------------- | -------------------------------- |
| `Cargo`           | Catálogo único de cargos docentes. `orden` registra la jerarquía institucional              | `designaciones.cargos`           |
| `Dedicacion`      | Catálogo seleccionable de Categorías 1–6; valores históricos fuera de catálogo sólo se leen | `designaciones.dedicaciones`     |
| `Periodo`         | Ventana de carga + rango de impacto. A lo sumo uno activo a la vez                          | `designaciones.periodos`         |
| `Pedido`          | **El trámite.** Cubre exactamente una materia — la cátedra del Jefe de Cátedra              | `designaciones.pedidos`          |
| `PedidoAdjunto`   | Documentación respaldatoria (CV, DNI, justificativo)                                        | `designaciones.pedido_adjuntos`  |
| `PedidoHistorial` | Línea de tiempo del trámite, con el rol con el que se actuó y el comentario                 | `designaciones.pedido_historial` |
| `Designacion`     | **El estado vigente** `(persona, materia, cargo, dedicación y tres horas)` con vigencia     | `designaciones.designaciones`    |

### Pedido vs Designación

Son las dos entidades centrales y quieren propiedades opuestas. Confundirlas es el error de modelado que este dominio evita:

|                        | Pedido (trámite)                       | Designación (estado)    |
| ---------------------- | -------------------------------------- | ----------------------- |
| Naturaleza             | Inmutable una vez enviado              | Mutable, tiene vigencia |
| Verdad que sostiene    | "Esto decía cuando se firmó"           | "Esto es cierto hoy"    |
| Vínculo con la persona | ID canónico **+** `snapshot` congelado | ID canónico             |
| Vida                   | Termina en un estado terminal          | Vive hasta una Baja     |

El `snapshot` se congela **al enviar** a revisión, no al crear. Es lo que le da valor probatorio al trámite: un pedido aprobado en Decanato tres meses después sigue diciendo qué cargo tenía el docente el día que se cargó. Si se recalculara, el documento reescribiría su propio pasado.

### Trazabilidad

Aprobar un pedido se traduce a escrituras sobre `designaciones` en una única transacción:

```
Alta        → INSERT de una designación nueva
Baja        → UPDATE de vigente_hasta sobre la vigente
Cambio      → actualiza la misma fila si inició en la fecha efectiva;
              en otro caso cierra la vigente y abre una nueva
Continuidad → se conserva la designación vigente y no crea pedido
```

`origen_pedido_id` en NULL significa **carga administrativa directa**: la pantalla de administración de docentes escribe esta misma tabla. Son dos caminos de escritura hacia una sola tabla, y esa columna es lo único que permite distinguirlos.

La cadena completa hacia atrás: designación vigente → `origen_pedido_id` → pedido → `pedido_historial` → `audit.change_log`.

### Un pedido, una materia

`identity.roles` define `jefe_catedra` con `scope = 'materia'`: cátedra **es** materia. El pedido lleva `materia_id` y `carrera_id` canónicos elegidos juntos. Una materia compartida no determina por sí sola la carrera: la carrera almacenada determina al Coordinador competente. `materias_plan` sólo valida el catálogo informativo; no es una FK obligatoria del pedido.

El formulario resuelve la materia en contexto: Alta usa las materias activas del
Jefe de Cátedra; Baja/Cambio intersectan esas materias con las designaciones
vigentes del docente seleccionado. Una única opción se fija automáticamente y
varias se eligen por UUID. El backend no confía en esa anticipación: valida el
ámbito y exige `(persona_id, materia_id)` vigente para Baja/Cambio. Snapshot,
horas y datos actuales se toman de esa designación, nunca de la primera del docente.

### Coordinación de administración y circuito

Administración de docentes y circuito de pedidos escriben las mismas vigencias; el comportamiento exigido está en [persistencia-designaciones](../../../openspec/specs/persistencia-designaciones/spec.md#requirement-mutaciones-de-vigencias-sin-pérdida-concurrente). Decisiones de implementación no obvias:

- La exclusión es transaccional en PostgreSQL (por persona y por par persona–materia), no un mutex del proceso, y se toma **antes** de leer el estado que se confirmará.
- El orden es siempre Identity primero (usuarios, personas) y después vigencias. Evita una inversión cuando administración edita la cuenta del mismo actor que está enviando su propio pedido.
- No hay retry ciego: una observación obsoleta devuelve conflicto.

### Listado y detalle

La grilla trae todas las filas y acciones, con un resumen acotado de eventos y sin adjuntos; el detalle trae historial completo y documentación. Contrato en [api-contracts-designaciones.md](../api-contracts-designaciones.md#lectura-de-grilla-versus-detalle).

## API pública (contract)

| Interfaz                       | Métodos                                             | Consumido por                      |
| ------------------------------ | --------------------------------------------------- | ---------------------------------- |
| `IAdministracionDesignaciones` | listar, validar y reemplazar designaciones vigentes | superficie administrativa del Host |

El contract transporta UUIDs y DTOs puros de asignación; no expone entidades EF, repositorios ni el `DesignacionesDbContext`. La administración de docentes puede coordinar persona, rol docente y designaciones sin adquirir una referencia al módulo interno.

Para un Alta de pedido, Designaciones consume por DI la frontera pública
`IAdministracionIdentity.CrearPersonaSinCuentaAsync`. La implementación vive en
Shared y sólo crea la persona canónica; Designaciones no accede al `IdentityDbContext`
ni a repositorios de Identity.

## Endpoints HTTP

| Recurso            | Path                                         | Autoridad                                                           |
| ------------------ | -------------------------------------------- | ------------------------------------------------------------------- |
| Períodos           | `/api/designaciones/periodos`                | permiso `periodos.administrar`                                      |
| Catálogos acotados | `/api/designaciones/catalogos`               | identidad y ámbitos persistidos                                     |
| Pedidos y detalle  | `/api/designaciones/pedidos[/{id}]`          | permisos y visibilidad resueltos por backend                        |
| Transiciones       | `/api/designaciones/pedidos/{id}/{accion}`   | máquina de estados, permiso de etapa e idempotencia                 |
| Lote XLSX          | `/api/designaciones/periodos/{id}/lote.xlsx` | Secretaría, Decanato o Administración departamental; período activo |

El contrato completo está en [api-contracts-designaciones.md](../api-contracts-designaciones.md).

## Reglas de negocio

Ver [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md).

BR-designaciones-001 es la única con implementación en la base: índice único parcial sobre `(periodo_id, persona_id)` excluyendo los estados `rechazado` y `cancelado`, más validación en el backend para el mensaje de error. La base es la autoridad — es lo único que sobrevive a dos requests concurrentes.

## Dependencias

- **Hacia `identity`**: lee mediante `IConsultasIdentity` para resolver ámbitos, personas y materias, y consume por DI `IAdministracionIdentity` para solicitar la creación de una persona sin cuenta en un Alta. La escritura concreta sigue siendo exclusiva de la administración de Shared; el módulo **no accede** a `IdentityDbContext` ni a repositorios.
- **Exportación**: el lote es una proyección de sólo lectura ([exportacion-lote-designaciones](../../../openspec/specs/exportacion-lote-designaciones/spec.md)); el correo de las altas sale de `IPortalQueries`.
- **Hacia adentro**: `Modules.Portal.Contracts` (correo del perfil para la hoja `ALTAS`) y `ArsDocendi.Storage.Contracts` (adjuntos de pedidos).
- **Hacia afuera**: el Host consume `IAdministracionDesignaciones` para la administración de docentes.
- **Externas**: ninguna.
