---
status: draft
owner: ""
feature: "openspec/specs/reserva-aulas/spec.md"
last_updated: 2026-09-20
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
6. Doble click sobre una fila Rechazada abre un popup de solo lectura con el detalle completo de la
   solicitud, incluido el motivo del rechazo. Las demás filas no reaccionan al doble click en esta
   tabla.

**Administrativo:**

1. Entra a `/aulas` y ve la sección "Todas las solicitudes": todas las solicitudes de todos los
   docentes, con el mismo detalle más el nombre del Docente. Esta tabla no tiene columna Acciones — la
   interacción es doble click sobre la fila.
2. Doble click sobre una fila Pendiente abre el modal "Asignar aula": indica el aula/laboratorio y
   confirma ("Asignar y aprobar"), o hace click en "Rechazar solicitud" (botón secundario del mismo
   modal) para pasar al flujo de rechazo en vez de aprobar. Asignar pasa la solicitud a Aprobada; el
   Docente la ve actualizada en su propia lista.
3. Al elegir "Rechazar solicitud" se cierra el modal de asignación y se abre "Rechazar solicitud": un
   motivo es obligatorio para confirmar. La solicitud pasa a Rechazada; el Docente ve el motivo
   haciendo doble click en su fila.
4. Doble click sobre una fila Aprobada abre el mismo modal de asignación en modo "Actualizar aula
   asignada", precargado con el aula actual — por si el Administrativo se equivocó o cambian las
   condiciones del examen. No cambia el estado, solo el aula. No ofrece la opción de rechazar (ya no
   está Pendiente).
5. Doble click sobre una fila Rechazada o Cancelada no hace nada: son estados terminales sin acción
   posible.

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
- **"Todas las solicitudes" (Administrativo) usa doble click en vez de columna Acciones** — ajuste
  posterior a la primera versión: un botón "Asignar aula" deshabilitado en dos de los tres estados
  ocupaba espacio sin aportar nada. El doble click resuelve las tres acciones (asignar / actualizar /
  nada) sin columna extra. Ver `openspec/changes/reserva-aulas/design.md` (decisión 8).
- **Actualizar el aula de una Aprobada no es "editar el pedido"**: es una corrección puntual del dato
  aula, no reabre el circuito ni permite tocar día/horario/materia/comisión — esos siguen siendo
  inmutables una vez aprobada.
- **Rechazar vive dentro del modal "Asignar aula", no como una tercera rama del doble click**: una fila
  Pendiente tiene dos desenlaces posibles (aprobar o rechazar) y conviene verlos juntos en el mismo
  modal en vez de que el Administrativo tenga que adivinar de antemano qué gesto dispara cuál acción.
  El botón "Rechazar solicitud" solo aparece en modo "asignar" (no en "actualizar": una Aprobada ya no
  es rechazable). Ver `openspec/changes/reserva-aulas/design.md` (decisión 9).
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
