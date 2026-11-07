---
status: approved
owner: "Julian Castellana"
feature: "../../../openspec/changes/conciliacion-designaciones/specs/conciliacion-designaciones/spec.md"
last_updated: 2026-11-06
---

# Design spec: Conciliación del lote

## Resumen

Pantalla `Designaciones / Conciliación` para quien tiene `designaciones.conciliar` (por defecto
Secretaría Académica y Administrativos). Cuando la rectificación vuelve con lo que se aprobó del
lote, el operador revisa por docente cómo queda cada uno, acepta a los que coinciden, rechaza con
motivo lo que no se aprobó, corrige lo que cambió y sincroniza. Recién al sincronizar se escribe la
designación y el rol Docente. El prototipo validado con el cliente está en el artifact
[Conciliación de designaciones](https://claude.ai/artifact/91D9Tx8cYkg5QMkpuxs5XA).

## Roles que ven esta surface

- [ ] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [x] Secretaría Académica
- [ ] Decanato
- [x] Administrativos
- [ ] Docente

Cualquier otro rol al que se le otorgue `designaciones.conciliar` también la ve. Editar un pedido
desde acá requiere además `designaciones.editar`.

## Flujo principal

1. El operador entra por la navegación (Designaciones → Conciliación). La pestaña **Por
   conciliar** lista una fila por docente con pedidos en lote del período activo.
2. Abre la fila y ve el detalle por materia: carrera, **Hoy** (designación vigente), **Queda**
   (lo pedido, con horas de investigación y externas), la novedad, el número de pedido, la
   justificación de un Cambio y el tipo de baja. Las materias sin pedido figuran "Sin cambios" y
   el pie resume con cuántas materias y horas queda.
3. Si algo no se aprobó, **Rechazar** pide el motivo; el pedido pasa a la pestaña
   **Rechazados**, sale del Excel y el docente queda sin aceptar.
4. Si algo cambió, **Editar** abre el formulario completo del pedido; al guardar vuelve a la
   conciliación y el docente queda sin aceptar.
5. Tilda a los docentes que coinciden (o todos los visibles) y usa **Sincronizar N cambios**,
   junto a las pestañas.
6. La vista previa muestra totales (docentes nuevos, altas, cambios, bajas), una línea por
   pedido con materia y carrera y una nota neutra con lo que se actualiza en la base
   (designaciones por materia y rol Docente) y que los pedidos ya no se podrán editar. Al confirmar, los
   pedidos pasan a **Sincronizados** con fecha y autor, y ya no se pueden editar.

## Layout / IA

- Migas `Inicio / Conciliación`, `PageHeader` con el período activo.
- `Pestanas` (Por conciliar, Rechazados, Sincronizados, con contador) y el botón primario
  Sincronizar a la derecha, sólo en Por conciliar.
- Barra de filtros: búsqueda por docente, CUIL o DNI; Novedad; Carrera sólo cuando hay más de una.
  A la derecha, la cuenta de lo visible.
- `Table` del sistema de diseño dentro de `ContenedorTabla`; fila expandible con una tabla
  interna de diff. `Checkbox`, `StatusBadge`, `NovedadChip`, `Button`, `InlineAlert`, `Field`,
  `Textarea` y el `Modal` de la app.
- En teléfono se ocultan las columnas Carrera y Materias y el detalle se desplaza en horizontal.

## Estados a diseñar

| Estado      | Descripción                                                                                                          | Cuándo se muestra                |
| ----------- | -------------------------------------------------------------------------------------------------------------------- | -------------------------------- |
| Loading     | Texto "Cargando la conciliación del período…"                                                                        | Carga inicial                    |
| Empty       | `InlineAlert` info por pestaña; en Por conciliar aclara que se ven únicamente los pedidos en lote del período activo | Sin pedidos en esa pestaña       |
| Sin filtro  | Fila "Sin docentes que cumplan los filtros."                                                                         | Los filtros no dejan nada        |
| Error       | `InlineAlert` danger con Reintentar; en los modales, el error dentro del diálogo                                     | Falla de red o servidor          |
| Conflicto   | `InlineAlert` warning "No se sincronizó nada" con el pedido que cambió; el docente queda sin aceptar                 | `409 conciliacion-pedido-cambio` |
| Sin período | `InlineAlert` info "No hay un período activo"                                                                        | No hay período activo            |
| Success     | Toast "Sincronización completa" con cantidad de cambios y docentes                                                   | Tras sincronizar                 |

## Decisiones de diseño

- **Se acepta por docente, no por pedido.** La rectificación se controla persona por persona:
  aceptar a un docente acepta todos sus pedidos en lote. Por eso un rechazo o una edición le quita
  la aceptación: lo aceptado deja de ser lo que se vio.
- **La aceptación es local y se ata a las versiones vistas.** No se persiste; sincronizar envía
  `id` y `version` de cada pedido, y el backend rechaza todo si alguno cambió.
- **Sin resaltados en el diff.** Hoy y Queda se leen lado a lado; el cliente pidió quitar los
  resaltados de lo que cambia.
- **Rechazados se editan para volver a conciliar.** El botón dice "Editar y volver a conciliar" y
  el formulario avisa "Vuelve a conciliarse". No hay otra salida para un rechazo en conciliación.
- **Sincronizado es definitivo.** La pestaña es un registro con fecha y autor, sin acciones.
- **El formulario de edición no ofrece "Guardar y enviar"** para pedidos en conciliación: ya
  pasaron el circuito.

## Anti-patterns a evitar (específicos de esta feature)

- Mostrar acciones que el backend no habilita: cada botón sale de `acciones` del pedido.
- Sincronizar sin vista previa, o sincronizar de a un pedido: la operación es todo o nada.
- Inventar estados intermedios ("aceptado") en la base: aceptar es una selección de la pantalla.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Spec funcional: [`openspec/changes/conciliacion-designaciones/specs/conciliacion-designaciones/spec.md`](../../../openspec/changes/conciliacion-designaciones/specs/conciliacion-designaciones/spec.md)
- Contrato: [`api-contracts-designaciones.md`](../../architecture/api-contracts-designaciones.md#conciliación-del-lote)

## Open questions de diseño

- Ninguna abierta.
