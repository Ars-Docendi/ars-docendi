---
status: draft
owner: ""
feature: "openspec/changes/notificaciones-in-app"
last_updated: 2026-11-02
---

# Design spec: Notificaciones in-app

## Resumen

Una campana en el shell avisa cuántas notificaciones sin leer tiene la persona bajo el rol con que
opera y abre un panel lateral donde las lee, las marca y salta a lo que las originó (una solicitud de
aula, un pedido de designación, una tarea). La referencia de aceptación es el prototipo
`notificaciones.html`; el correo queda fuera.

## Roles que ven esta surface

- [x] Jefe de Cátedra
- [x] Coordinador de Carrera
- [x] Secretaría Académica
- [x] Decanato
- [x] Administrativos
- [x] Docente

Todos los usuarios autenticados, sin permiso propio: cada uno ve sólo lo suyo, y sólo lo dirigido al
rol con que está operando (las tareas no tienen rol y se ven bajo cualquiera).

## Flujo principal

1. La campana muestra la cantidad de no leídas (se oculta en 0, `99+` por encima de 99) y se
   actualiza sola cada 60 s y al volver a la ventana si pasaron más de 30 s.
2. Al hacer click se abre el panel a la izquierda del contenido, pegado a la barra lateral. Abre en
   "No leídas".
3. Click en una notificación: se marca como leída, el panel se cierra, la app va a la entidad
   (`/aulas/solicitudes/:id`, `/designaciones/pedidos/:id`, `/tareas/:id`) y la resalta unos segundos.
4. "Marcar como leídas" abre el modo selección (casillas, "Seleccionar todas", pie con la cantidad).
   "Cancelar" lo cierra sin cambios.

## Layout / IA

- **Escritorio**: la campana va en el pie de la barra lateral, entre "Centro de ayuda" y la tarjeta de
  usuario. Expandida: ícono, etiqueta "Notificaciones" y píldora con la cantidad. Colapsada: sólo el
  ícono con una insignia en la esquina y un tooltip "Notificaciones".
- **Pantallas angostas (< 768 px)**: la campana va a la derecha de la barra superior (ícono con
  insignia); el panel cubre toda la app.
- **Panel**: 500 px de ancho máximo (nunca más que la pantalla), título "Notificaciones", botón de
  cerrar, pestañas "No leídas" / "Leídas" (teclado: flechas, Inicio y Fin) y grupos "Hoy", "Esta
  semana" (la semana empieza el lunes) y "Anteriores". 20 por página, carga al llegar al final.
- **Ítem**: ícono por evento, título (negrita si no leída), mensaje, enlace ("Ir a la solicitud →",
  "Ir al pedido →", "Ir a la tarea →") y una descripción sólo para lectores de pantalla.

## Estados a diseñar

| Estado             | Descripción                                                                                              | Cuándo se muestra                 |
| ------------------ | -------------------------------------------------------------------------------------------------------- | --------------------------------- |
| Loading            | Esqueleto de tres filas (`role=status`, "Cargando notificaciones")                                       | Primera carga de una pestaña      |
| Empty "No leídas"  | "No tenés notificaciones sin leer" / "Las nuevas notificaciones van a aparecer acá."                     | Sin no leídas                     |
| Empty "Leídas"     | "Todavía no tenés notificaciones leídas" / "Cuando abras una notificación, la vas a encontrar acá."      | Sin leídas                        |
| Error              | "No pudimos cargar las notificaciones" / "Revisá tu conexión e intentá nuevamente." + "Reintentar"       | Falla la primera carga            |
| Error de refetch   | Aviso "No pudimos actualizar la lista." + "Reintentar", la lista previa se conserva                      | Falla una actualización con datos |
| Error de siguiente | "No pudimos cargar más notificaciones." + "Reintentar"                                                   | Falla la página siguiente         |
| Rollback           | Toast "No pudimos marcar la notificación como leída" (o "…las notificaciones como leídas") y se restaura | Falla marcar como leída           |

## Decisiones de diseño

- **Marcar como leída es optimista** aunque las transiciones no lo sean: sólo cambia el estado de
  lectura de quien la mira, es idempotente y un error cuesta un toast y un rollback. Ver
  `openspec/changes/notificaciones-in-app/design.md`, decisión 7.
- **El perfil activo manda**: cambiar de rol cambia la campana y el panel (el rol es parte de toda
  consulta). Un aviso dirigido a otro de tus roles no aparece hasta que cambies a ese rol.
- **"Seleccionar todas" incluye lo que no se cargó**: se envía "todas hasta el instante de la más nueva
  cargada", así lo que llegue mientras tanto no se marca sin haberse visto.
- **El panel participa de la pila de capas**: Esc y el atrapado de foco actúan sobre la capa superior,
  igual que los diálogos de formulario. Al cerrar por navegación el foco no vuelve a la campana.
- **Atrás cierra el panel**: con el panel abierto, el botón Atrás del navegador lo cierra y deja la
  página donde está (mecanismo común de capas, `navegacion-consistente`). Abrir una notificación
  navega sin origen: el detalle usa su destino fijo en "Volver".
- **Pantallas angostas**: por debajo de 560 px el panel ocupa todo el ancho y el botón de cierre mide 44 px.
- **Toasts**: un host propio de la app (`shared/ui/Toaster`) porque el `Toast` de la librería es sólo
  una tarjeta.
