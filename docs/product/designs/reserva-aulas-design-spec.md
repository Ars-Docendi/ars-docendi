---
status: draft
owner: ""
feature: "openspec/specs/reserva-aulas/spec.md"
last_updated: 2026-11-02
---

# Design spec: Reserva de Aulas

## Resumen

Pantalla `/aulas` donde el Docente genera y da seguimiento a sus propias solicitudes de reserva de
aula/laboratorio para mesas de examen, y el Administrativo ve todas las solicitudes del departamento y
les asigna aula (aprobándolas). Reemplaza el stub "Módulo en construcción" — es la primera pantalla
real del módulo Aulas.

## Roles que ven esta surface

- [ ] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [x] Administrativos
- [x] Docente

La pantalla se gatea por permiso (`aulas.solicitar` / `aulas.aprobar`), no por nombre de rol — un rol
institucional o personalizado que tenga alguno de esos permisos ve la sección correspondiente.

## Flujo principal

**Docente:**

1. Entra a `/aulas` y ve la sección "Mis solicitudes": una tabla con todas sus solicitudes, estado y
   (si está Aprobada) el aula asignada.
2. Hace click en "Nueva solicitud", completa día, horario desde/hasta, cantidad aproximada de alumnos,
   materia y comisión, y confirma.
3. La solicitud aparece en la tabla en estado Pendiente.
4. Si se arrepiente y la solicitud sigue Pendiente, la cancela desde la fila (botón "Cancelar" →
   confirmación → pasa a Cancelada).
5. Cuando Administrativo asigna aula, la fila pasa a Aprobada y muestra el aula asignada. Cuando la
   rechaza, la fila pasa a Rechazada.
6. Un click en cualquier fila (o Enter con el foco en ella) abre la página de detalle de la solicitud
   (`/aulas/solicitudes/:id`): todos los datos y, si corresponde, el motivo del rechazo o de la
   cancelación. La celda principal de la fila es un enlace real al detalle. "Cancelar" de la fila sigue
   cancelando sin abrir el detalle. Desde el detalle, el Docente puede cancelar su solicitud Pendiente
   ("Tu solicitud"); una Aprobada no admite acciones (se pide a Administración).

**Administrativo:**

1. Entra a `/aulas` y ve la sección "Todas las solicitudes": todas las solicitudes de todos los
   docentes, con el mismo detalle más el nombre del Docente. Esta tabla no tiene columna Acciones — un
   click en cualquier fila abre el detalle de la solicitud.
2. En el detalle de una solicitud Pendiente, el panel "Acciones" pide el aula/laboratorio y confirma
   ("Asignar y aprobar"), o "Rechazar solicitud" abre un diálogo con motivo obligatorio. Asignar pasa la
   solicitud a Aprobada; el Docente la ve actualizada en su propia lista.
3. Al rechazar, un motivo es obligatorio para confirmar. La solicitud pasa a Rechazada; el Docente ve el
   motivo en el detalle de su solicitud.
4. En el detalle de una Aprobada, el mismo panel permite "Actualizar aula" (precargada con el aula
   actual — no cambia el estado) o "Cancelar reserva": un diálogo con motivo obligatorio que pasa la
   solicitud a Cancelada, libera el aula y deja el motivo visible para el Docente.
   El diálogo avisa "El aula asignada queda liberada, el docente ve el motivo completo y se le notifica
   (in-app)." — es cierto: el Docente recibe la notificación "Reserva cancelada".
5. El detalle de una Rechazada o Cancelada muestra el motivo y avisa "Esta solicitud está cerrada: no
   admite más acciones."

## Layout / IA

Una sola pantalla `/aulas` con dos secciones apiladas (cuando el actor tiene ambos permisos, ve las
dos; en la matriz actual son mutuamente excluyentes: Docente ve solo "Mis solicitudes", Administrativo
ve solo "Todas las solicitudes"). Cada sección es un header con título (+ botón "Nueva solicitud" en
"Mis solicitudes") y una tabla con filtro/orden por encabezado, mismo patrón visual que
`/designaciones/mis-pedidos`.

## Estados a diseñar

| Estado  | Descripción                                                                            | Cuándo se muestra                      |
| ------- | -------------------------------------------------------------------------------------- | -------------------------------------- |
| Loading | Texto "Cargando…" bajo el header de la sección                                         | Carga inicial de cada lista            |
| Empty   | Alerta informativa ("Todavía no generaste solicitudes" / "Todavía no hay solicitudes") | Lista vacía (sin datos, no por filtro) |
| Error   | Alerta de error con botón "Reintentar"                                                 | Falla la consulta                      |
| Success | Tabla con filas, filtro y orden por encabezado                                         | Estado normal con datos                |
| Detalle | Cargando la solicitud…, error "No se pudo cargar la solicitud" o datos + acciones      | `/aulas/solicitudes/:id`               |

Un caso adicional no listado en la tabla del template: filtros sin coincidencias (lista tiene datos
pero el filtro activo no matchea ninguno) muestra una alerta "Sin resultados" separada del Empty real,
igual que en Mis Pedidos.

## Decisiones de diseño

- **Aula asignada es texto libre**, no un selector de un catálogo: no existe todavía un catálogo de
  aulas/laboratorios (capacidad, equipamiento) — es responsabilidad futura de Secretaría Académica
  ("Configurables del módulo", fuera de alcance de este change).
- **Materia es un desplegable acotado a las materias del docente** (ajuste posterior a la primera
  versión, que la pedía como texto libre): sale de `identity.materias` vía las membresías del docente,
  no de un catálogo abierto ni de texto libre — evita typos y solicitudes sobre materias que el docente
  no dicta. El backend vuelve a validar, no confía en que el desplegable ya acotó. Comisión sigue
  siendo texto libre: no hay catálogo de comisiones.
- **"Mis solicitudes" (Docente) conserva la columna Acciones**: "Cancelar" queda visible pero
  deshabilitado cuando la solicitud no está Pendiente, mismo lenguaje que Mis Pedidos de Designaciones
  (acciones fijas en la fila, atenuadas cuando no aplican).
- **Las dos tablas abren un detalle con un click en la fila** (reemplaza el doble click por estado de
  "Todas las solicitudes", decisión 8 de `openspec/changes/reserva-aulas/design.md`, superada por
  `openspec/changes/reserva-aulas-detalle-y-cancelacion/`): un solo gesto para todas las filas, un enlace
  real en la celda principal (teclado, click medio, barra de estado) y la resolución en una página con
  dirección propia, que es el destino de las notificaciones.
- **Actualizar el aula de una Aprobada no es "editar el pedido"**: es una corrección puntual del dato
  aula, no reabre el circuito ni permite tocar día/horario/materia/comisión — esos siguen siendo
  inmutables una vez aprobada.
- **Cada cambio avisa**: asignar aula, actualizarla (sólo si cambia), rechazar y cancelar la reserva
  generan una notificación para el Docente dueño; que el Docente cancele una pendiente avisa a
  Administrativo y Secretaría Académica (si ya pidieron un aula afuera del sistema, hay que liberarla).
  Cada aviso lleva a `/aulas/solicitudes/:id` (ver `notificaciones-design-spec.md`).
- **Rechazar y Cancelar reserva viven junto a "Asignar" en el panel de acciones del detalle**: una
  Pendiente tiene dos desenlaces (aprobar o rechazar) y una Aprobada dos (actualizar el aula o cancelar
  la reserva), siempre a la vista. "Rechazar solicitud" sólo aparece en una Pendiente y "Cancelar
  reserva" sólo en una Aprobada. Ver `openspec/changes/reserva-aulas/design.md` (decisión 9).
- **Diálogos con la carcasa compartida de la app** (`ModalDeFormulario`): 480 px para confirmaciones,
  `min(600px, 100vw - 32px)` para formularios (con el zoom de la app), hoja a pantalla completa debajo de
  560 px, foco inicial en el primer campo (o en el botón menos destructivo), foco de vuelta al abridor,
  Esc sólo sobre el diálogo superior, sin cierre mientras se guarda y confirmación inline "Tenés cambios
  sin guardar." con "Seguir editando" / "Descartar cambios".
- **"Nueva solicitud"**: secciones Examen / Fecha y horario / Capacidad, controles de 44 px, día como
  `dd/mm/aaaa` con botón "Abrir calendario" y horarios `hh:mm` de 24 h (nada de mm/dd/yyyy ni AM/PM),
  error por campo con foco en el primero inválido y formulario vacío en cada apertura.
- **El motivo de rechazo es obligatorio**, mismo patrón que "Rechazar pedido" en Designaciones: sin
  motivo no hay forma de confirmar, y el backend revalida (no confía en la validación del formulario).
- **Cód. Materia se agrega como columna de solo lectura** (sin filtro/orden propios: el filtro de
  Materia ya cubre la búsqueda) en ambas tablas — el dato ya viajaba en `MateriaOpcion.codigo`, solo no
  se mostraba. Ver decisión 10.
- **Renombres de encabezado sin tocar el dato**: "Alumnos aprox." → "Capacidad", "Aula asignada" →
  "Aula" en ambas tablas.

## Anti-patterns a evitar (específicos de esta feature)

- No mostrar un selector de aulas con opciones inventadas — sería fake UI (invariante #7) sin un
  catálogo real detrás.
- No dejar que el Docente edite una solicitud Aprobada o Cancelada: son estados terminales del circuito
  de dos pasos, no hay "deshacer" en esta pantalla (la actualización de aula es una acción exclusiva
  del Administrativo, no del Docente).

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Spec funcional: [`openspec/changes/reserva-aulas/specs/reserva-aulas/spec.md`](../../../openspec/changes/reserva-aulas/specs/reserva-aulas/spec.md)
- Patrón de tabla de referencia: `/designaciones/mis-pedidos` (`TablaMisPedidos`, filtro por
  encabezado con `FiltroEncabezado`).

## Open questions de diseño

- ¿Debería "Asignar aula" validar contra una capacidad conocida del aula vs. la cantidad aproximada de
  alumnos? Requiere el catálogo de aulas todavía inexistente — a confirmar con el cliente si se prioriza.
