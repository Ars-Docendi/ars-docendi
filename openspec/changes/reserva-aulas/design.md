## Context

`Modules.Aulas` es hoy un skeleton generado por `/create-module`: `AulasDbContext` sin entidades,
`AulasController` con solo `ping`, `IAulasQueries` vacío, y el frontend (`features/aulas/`) muestra un
stub. `docs/architecture/domains/aulas.md` dice explícitamente que las entidades quedan "a definir en
spec inicial del módulo" — este change es esa primera definición.

El catálogo de permisos de Aulas **ya existe como dato** en
`database/identity/007_identity_permisos.sql` (`aulas.ver`, `aulas.gestionar`, `aulas.aprobar`), pero
**no existe como código**: `ArsDocendi.Shared/Auth/Permisos.cs` (la clase que declara las constantes
usadas en `[Authorize(Policy = Permisos.X)]` y cuyo array `Todos` registra las policies de ASP.NET en
`ArsDocendi.Host/Program.cs:39`) no tiene ninguna entrada de Aulas. Sin esto, ningún endpoint de Aulas
puede autorizar por permiso todavía — es deuda pendiente desde que se sembró el catálogo SQL, y hay
precedente de resolverlo así (change archivado `2026-09-09-unificar-roles-permisos-navegacion-dinamica`,
tarea 1.1, agregó `docentes.ver` de la misma forma: SQL + `Permisos.Todos` + verificación por
integración).

La matriz `database/identity/008_identity_rol_permisos.sql` está explícitamente marcada en el propio
archivo como PROVISIONAL/pendiente de confirmación con el cliente. Hoy el rol `docente` no tiene ningún
permiso de Aulas (solo `jefe_catedra` tiene `aulas.ver`), lo cual no permite lo que pide esta feature.

El dominio Aulas declara alcance explícito: "Solo exámenes" (no clases regulares) y bounded context que
**excluye** "información académica de las materias" — la materia/comisión de un examen no tiene hoy
ningún catálogo propio en Aulas ni referencia cross-module contemplada para este change.

## Goals / Non-Goals

**Goals:**

- Modelar y persistir la solicitud de reserva de aula como primera entidad real del schema `aulas`.
- Dar de alta el permiso `aulas.solicitar` (código + SQL) y completar en código los tres permisos de
  Aulas que ya existían solo como dato (`aulas.ver`, `aulas.gestionar`, `aulas.aprobar`), habilitando
  policies reales.
- Exponer el ciclo completo por API: crear, listar propias, listar todas, cancelar, asignar aula.
- Construir la pantalla `/aulas` con las dos vistas (Docente / Administrativo) sobre una tabla
  ordenable/filtrable reutilizando los componentes compartidos ya usados en Designaciones.

**Non-Goals:**

- Catálogo de aulas/laboratorios (código, capacidad, equipamiento) — el dominio ya anticipa esa
  superficie como "Configurables del módulo" de Secretaría Académica, fuera de esta ronda. El aula
  asignada se captura como texto libre (p. ej. "Lab 3", "Aula 204"), no como referencia a una tabla de
  aulas.
- Vista de Aulas para Coordinador/Secretaría/Decanato — el pedido del usuario acota explícitamente a
  Docente y Administrativo.
- Comisión sigue siendo texto libre, sin catálogo ni validación cruzada (a diferencia de Materia, que
  sí quedó acotada a `identity.materias` del docente tras la revisión — ver decisión 3).
- Resolución de conflictos de horario/superposición de aulas — no lo pidió el usuario; se deja para una
  iteración posterior si surge como requerimiento.
- Reglas de negocio con cita normativa institucional — no se identificó ninguna para esta ronda.

## Decisions

### 1. Nueva entidad `SolicitudReservaAula` en el schema `aulas`

DDL real en `database/aulas/001_aulas_solicitudes.sql` (patrón `RecursosSql`/`ExcludeFromMigrations`,
igual que Designaciones — ver `docs/architecture/data-model.md`). Campos: `id` (uuid pk),
`docente_id` (uuid, la persona solicitante — ver decisión 4), `dia` (date), `horario_desde` / `horario_hasta`
(time), `cantidad_alumnos_aprox` (int), `materia` (text), `comision` (text), `estado`
(`pendiente` | `aprobada` | `cancelada`), `aula_asignada` (text, nullable — se completa recién al
aprobar), `created_at`, `updated_at`. Un `CHECK` en base garantiza `horario_hasta > horario_desde` y
`cantidad_alumnos_aprox > 0` — la misma regla que hoy el frontend valida se repite en el DDL, siguiendo
el principio de "frontend anticipa, backend es la autoridad" ya aplicado a Pedidos de Designación.

**Alternativa considerada**: modelar el estado como tabla de eventos/historial (como
`pedidos-designacion`, que sí tiene un circuito multi-etapa con devoluciones). Rechazada: acá el
circuito es de dos pasos sin retorno (`pendiente` → `aprobada` o `pendiente` → `cancelada`, ambos
terminales), no amerita la complejidad de un historial de eventos.

### 2. Estado sin campo de eventos separado

`estado` es una columna simple, no una máquina de estados con historial (a diferencia de
`MaquinaEstadosPedido`). Transiciones válidas: `pendiente → aprobada` (solo Administrativo, vía asignar
aula) y `pendiente → cancelada` (solo el Docente dueño, y solo si sigue `pendiente`). Una solicitud
`aprobada` no se puede cancelar desde esta pantalla (fuera de alcance: si se necesita, es una iteración
futura de "reprogramar/liberar aula"). Sí se puede **actualizar el aula asignada** de una solicitud ya
`aprobada` (mismo endpoint que asignar, sin transición de estado — ver decisión 8): es una corrección de
dato, no una reapertura del circuito.

### 3. Materia como catálogo acotado a `identity.materias`; Comisión y Aula asignada como texto libre

**Actualizada tras revisión** — versión original: Materia y Comisión texto libre, ver historial. El
usuario pidió que Materia sea un desplegable acotado a las materias que el docente solicitante tiene
asignadas, en vez de texto libre (permitía escribir cualquier cosa, con typos, sin relación con lo que
el docente realmente dicta).

La solución NO reabre el acoplamiento a Designaciones: `identity.materias` es el catálogo transversal
del que Designaciones también depende (`designaciones.pedidos.materia_id` referencia la misma tabla),
vive en el schema `identity` (infraestructura, no un módulo de negocio — invariante #4 enmendado), y
Aulas ya lee `identity` para todo lo demás (`ObtenerPersonaAsync`, `ListarUsuariosAsync`). Se resuelve
igual que Designaciones acota materias al Jefe de Cátedra: `IConsultasIdentity.ObtenerMateriasDeRolAsync
(usuarioId, rolActivo, ct)`, salvo que acá no se fija el rol a `"jefe_catedra"` — se usa el rol activo
de la sesión (`ICurrentUser.Roles.FirstOrDefault()`), porque tanto `docente` como `jefe_catedra` tienen
`aulas.solicitar` y ambos pueden tener materias asignadas en `identity.user_roles` (confirmado en el
seed sintético: el rol `docente` de un usuario también puede llevar `materia_id`).

`SolicitudReservaAula.MateriaId` (antes `Materia` string) es ahora un `Guid` con FK real a
`identity.materias(id) ON DELETE RESTRICT` — misma excepción cross-schema que
`designaciones.pedidos.materia_id`. El backend es la autoridad: `CrearAsync` valida que el
`materiaId` recibido esté en la lista de materias propias del actor, sin confiar en que el desplegable
del frontend ya lo acotó.

Comisión y Aula asignada siguen siendo texto libre: no hay catálogo de comisiones ni de
aulas/laboratorios (ver Non-Goals), y nada de lo pedido en esta revisión los afecta.

### 4. Identificación del solicitante vía `IConsultasIdentity` (no `Modules.Portal.Contracts`)

`docente_id` se persiste como el `personaId` del usuario autenticado (mismo patrón que
`identity.personas` en Designaciones), resuelto igual que `ServicioPortal.PersonaActualAsync`:
`ICurrentUser.UserId` → `IConsultasIdentity.ListarUsuariosAsync` → `Usuario.Persona.Id`. Para mostrar
nombre/legajo en la lista de "Todas las solicitudes" (vista Administrativo), el servicio de Aulas
consulta `IConsultasIdentity.ObtenerPersonaAsync(docenteId, ct)` — ya expone `Nombre`, `Apellido` y
`Legajo` directamente.

**Alternativa considerada y descartada durante la implementación**: el proposal original (y una
primera versión de este design) planteaba resolverlo vía `Modules.Portal.Contracts.IPortalQueries`,
materializando el edge `Aulas → Portal.Contracts` que `dependency-graph.md` ya anticipaba como TBD.
Se descartó al notar que `IConsultasIdentity` (en `ArsDocendi.Shared`, que todo módulo ya referencia)
expone los mismos datos sin agregar ninguna dependencia cross-module nueva — es el mismo mecanismo que
usa `ServicioPortal` para resolverse a sí mismo, y evita acoplar Aulas a la superficie pública de Portal
solo para leer nombre/legajo. El edge anticipado en `dependency-graph.md` queda sin materializar; si en
el futuro Aulas necesita algo que sólo Portal expone (áreas de experticia, horas disponibles), se agrega
recién ahí.

### 5. Permisos: completar `Permisos.cs` + un permiso nuevo `aulas.solicitar`

Se agregan a `ArsDocendi.Shared/Auth/Permisos.cs` (y a `Todos`): `AulasVer` (`aulas.ver`),
`AulasSolicitar` (`aulas.solicitar`, nuevo), `AulasGestionar` (`aulas.gestionar`, existente sin usar) y
`AulasAprobar` (`aulas.aprobar`, existente sin usar). Mapeo a endpoints:

| Endpoint                                    | Policy           | Quién                        |
| ------------------------------------------- | ---------------- | ---------------------------- |
| `GET /api/aulas/solicitudes/mias`           | `AulasSolicitar` | Docente (propias)            |
| `POST /api/aulas/solicitudes`               | `AulasSolicitar` | Docente (crear)              |
| `POST /api/aulas/solicitudes/{id}/cancelar` | `AulasSolicitar` | Docente, dueño de la fila    |
| `GET /api/aulas/solicitudes`                | `AulasAprobar`   | Administrativo (todas)       |
| `POST /api/aulas/solicitudes/{id}/asignar`  | `AulasAprobar`   | Administrativo (asigna aula) |

`aulas.gestionar` queda declarado en código (cierra la deuda de `Permisos.cs`) pero sin un endpoint
propio en esta ronda — ya está asignado a `administrativo`/`secretaria` en la matriz SQL para uso
futuro (p. ej. el configurable de aulas/laboratorios, fuera de alcance acá). No se inventa un endpoint
solo para darle uso.

**Alternativa considerada**: reusar `aulas.gestionar` para que el Docente solicite (su descripción SQL
dice "Solicitar y asignar..."). Rechazada: mezclaría en un solo permiso una acción de self-service
(Docente sobre lo propio) con una de administración (Administrativo sobre todo), rompiendo la simetría
que el propio catálogo ya usa en Designaciones (`designaciones.gestionar` vs. `designaciones.revisar`) y
obligando a Administrativo a heredar por accidente la capacidad de "solicitar como si fuera Docente".

### 6. Ámbito (propio vs. todo) resuelto por permiso, no por rol

A diferencia de `ResolutorActor` en Designaciones (que resuelve ámbito por código de rol de sistema
porque el circuito tiene 3 etapas con reglas por rol), acá el ámbito es binario y se resuelve
directamente por policy: el endpoint "mías" filtra por `docente_id = usuario actual` y el endpoint
"todas" no filtra. No hace falta un `ActorContexto` nuevo: cada acción ya tiene su propio policy
(`AulasSolicitar` vs `AulasAprobar`) y su propio repositorio/método, sin rama condicional por rol dentro
de un mismo endpoint. Es más simple porque solo hay dos personas, no un circuito de aprobación.

### 7. Frontend: dos vistas dentro de la misma pantalla `/aulas`, gateadas por permiso de sesión

`IndexPage.tsx` deja de ser un stub y renderiza `VistaMisSolicitudes` si la sesión tiene
`aulas.solicitar`, y/o `VistaTodasLasSolicitudes` si tiene `aulas.aprobar` — igual que
`navegacion-por-permisos` ya hace para la sidebar (permiso, no nombre de rol). Un usuario con ambos
permisos vería ambas vistas (no es el caso de la matriz actual, pero no se cierra la puerta). Reuso de
`shared/ui/FiltroEncabezado.tsx` para orden/filtro por columna, igual patrón que
`TablaMisPedidos`.

### 8. "Todas las solicitudes": doble click en la fila en vez de columna Acciones

Ajuste posterior a la primera implementación, pedido en revisión. La columna Acciones con un botón
"Asignar aula" quedaba rara para Administrativo: en `Pendiente` estaba habilitado, pero en `Aprobada` y
`Cancelada` quedaba deshabilitado ocupando espacio sin aportar nada (a diferencia de "Mis solicitudes",
donde Cancelar sí tiene sentido visualmente atenuado). Se reemplaza por doble click en la fila, con
comportamiento por estado:

- `Pendiente` → abre el modal "Asignar aula" (crear la asignación, transiciona a `Aprobada`).
- `Aprobada` → abre el mismo modal en modo "Actualizar aula asignada" (mismo endpoint, sin transición
  de estado — decisión 2), precargado con el aula actual.
- `Cancelada` → no hace nada; es un estado terminal sin acción posible.

`ModalAsignarAula` pasa a tomar un modo (`asignar` | `actualizar`) que solo cambia título, texto del
botón y el valor inicial del input — no hay dos componentes. La tabla "Mis solicitudes" del Docente NO
cambia: ahí Cancelar sigue siendo un botón en la columna Acciones, porque el pedido del usuario fue
específico para la vista de Administrativo.

### 9. Rechazar solicitud con motivo (nuevo estado `Rechazada`)

Ajuste pedido por el usuario en revisión: hoy una solicitud `Pendiente` solo puede terminar en
`Aprobada` (asignar aula) o `Cancelada` (el propio Docente). No hay forma de que Administrativo la
rechace explicando por qué. Se agrega un tercer desenlace terminal: `Rechazada`, con motivo obligatorio.

- Nuevo estado `rechazada` en el CHECK `solicitudes_reserva_estado_valido` y en
  `EstadosSolicitudAula`.
- Nueva columna `motivo_rechazo TEXT NULL` con un CHECK espejo del que ya existe para
  `aula_asignada`: obligatorio si y solo si `estado = 'rechazada'`.
- Transición válida: `pendiente → rechazada` (solo Administrativo, solo si `Pendiente`); no hay
  `aprobada → rechazada` ni `rechazada → *` (terminal, igual que `cancelada`). `AsignarAulaAsync`
  pasa a rechazar tanto `Cancelada` como `Rechazada`.
- Nuevo endpoint `POST /api/aulas/solicitudes/{id}/rechazar` (`AulasAprobar`), body `{ motivo }`,
  responde Conflicto si la solicitud no está `Pendiente`.
- Frontend (Administrativo): en vez de agregar una tercera rama al doble click (que ya reemplaza la
  columna Acciones — decisión 8), el rechazo se ofrece como acción secundaria **dentro** del modal
  "Asignar aula" que ya se abre al doble click sobre una fila `Pendiente`: un botón secundario
  destructivo "Rechazar solicitud" cierra ese modal y abre `ModalRechazarSolicitud` (`Textarea` de
  motivo obligatorio — mismo patrón que `ModalConfirmacionAccion`/`PanelAccionesRevision` de
  Designaciones, que ya resuelve "rechazar con justificativo obligatorio"). El botón no aparece en modo
  "actualizar" (una `Aprobada` ya no es rechazable).
- Frontend (Docente, "Mis solicitudes"): una fila `Rechazada` responde a doble click abriendo
  `ModalDetalleSolicitud`, un popup de solo lectura con los datos de la solicitud y el motivo de
  rechazo — mismo mecanismo de doble click que "Todas las solicitudes" ya usa (decisión 8), aplicado
  acá por primera vez en la tabla del Docente, y solo para el estado `Rechazada`: las demás filas de
  esta tabla no reaccionan al doble click (a diferencia de Administrativo, donde `Pendiente` y
  `Aprobada` sí lo hacen).
- `EstadoSolicitudPill`: nueva entrada `rechazada` (tono "peligro", ícono nuevo `IconoCircleX` en
  `lucide.tsx` para distinguirla visualmente de `cancelada`, que ya usa `IconoBan`).

**Alternativa considerada**: agregar `rechazada` como tercera rama del doble click en la fila (junto a
`pendiente`→asignar y `aprobada`→actualizar), pidiendo el motivo en un modal aparte abierto
directamente desde la fila. Rechazada: el doble click ya tiene semántica "1 fila = 1 acción"
(decisión 8); una fila `Pendiente` tiene ahora dos desenlaces posibles (aprobar o rechazar), y elegir
entre ellos amerita verlos juntos en el mismo modal en vez de que el usuario tenga que adivinar de
antemano cuál gesto dispara cuál acción.

### 10. Cód. Materia visible y renombres de columna en ambas grillas

Pedido de usuario en revisión, sin impacto en el modelo de datos: `MateriaOpcionDto`/`MateriaOpcion`
ya traen `codigo` desde la introducción del desplegable acotado (decisión 3) — hoy solo se muestra
`nombre`. Se agrega una columna "Cód. Materia" (antes de "Materia") en `TablaMisSolicitudes` y
`TablaTodasLasSolicitudes`, sin tocar backend. No se la hace filtrable/ordenable por separado: el
filtro existente de "Materia" ya cubre el caso de uso (buscar la materia), y agregar un segundo
control de filtro solo para el código sería una superficie sin pedido explícito del usuario.

Además, dos renombres de encabezado puramente visuales, sin cambiar el dato subyacente: "Alumnos
aprox." → "Capacidad" y "Aula asignada" → "Aula". El campo interno (`cantidadAlumnosAprox`,
`aulaAsignada`, columnas de base, nombres de variables) no se renombra: es un cambio de etiqueta de
columna en la grilla, no del dominio.

## Risks / Trade-offs

- **[Riesgo] Materia/Comisión en texto libre puede desalinearse con los nombres reales del catálogo
  académico** (typos, nombres distintos a los de Designaciones) → _Mitigación_: aceptado como
  limitación conocida para esta ronda (Non-Goal explícito); si se vuelve un problema real se propone un
  change de seguimiento para referenciar el catálogo de Designaciones vía Contracts.
- **[Riesgo] `Permisos.cs` es compartido (`ArsDocendi.Shared`) y lo tocan varios módulos** → _Mitigación_:
  el cambio es aditivo (nuevas constantes + nuevas entradas en `Todos`), no se renombra ni se quita
  nada existente; no hay consumidores que puedan romperse.
- **[Riesgo] La matriz de permisos por rol es PROVISIONAL y pendiente de confirmación con el cliente** →
  _Mitigación_: el ajuste que hace este change (agregar `aulas.ver` + `aulas.solicitar` a `docente` y
  `jefe_catedra`) es mínimo y reversible (una migración de datos), documentado en el proposal para que
  quede visible en la revisión.
- **[Trade-off] Sin catálogo de aulas, "asignar aula" no valida disponibilidad ni duplicados** (dos
  solicitudes podrían recibir la misma aula en el mismo horario) → aceptado como limitación conocida;
  Administrativo es responsable humano de no duplicar, igual que el proceso manual actual que esta
  feature reemplaza.

## Migration Plan

1. Agregar el DDL `database/aulas/001_aulas_solicitudes.sql` y la migración EF que lo invoca
   (`ExcludeFromMigrations`, igual que Designaciones).
2. Agregar la fila de permiso `aulas.solicitar` a `database/identity/007_identity_permisos.sql` (nuevo
   archivo numerado siguiente, no editar el ya aplicado) y actualizar
   `database/identity/008_identity_rol_permisos.sql` para `docente`/`jefe_catedra`.
3. Completar `Permisos.cs` (aditivo).
4. Implementar Domain/Repository/Service/Api del lado backend.
5. Reemplazar el frontend stub.
6. Actualizar docs (`domains/aulas.md`, `data-model.md`) en el mismo PR.

**Rollback**: ninguna de estas piezas tiene consumidores productivos hoy (`IAulasQueries` es un
placeholder vacío, sin nadie que lo importe); revertir el PR completo es seguro. Si solo se necesita
revertir el ajuste de permisos, es una migración de datos independiente del código.

## Open Questions

- ¿"Cantidad aproximada de alumnos" debe validarse contra la capacidad del aula asignada? Hoy no hay
  catálogo de aulas con capacidad, así que no se puede validar — queda documentado como Non-Goal, a
  confirmar con el cliente si en el futuro se agrega el catálogo de aulas/laboratorios.
- ¿La matriz provisional de permisos (paso 2 del Migration Plan) requiere aprobación explícita de
  Secretaría Académica antes de aplicarse a producción, dado que está marcada PROVISIONAL? Se asume que
  no bloquea este change (el propio comentario en el SQL dice que se ajusta "sin migración" desde
  `/roles"), pero se deja anotado para que el revisor lo confirme.
