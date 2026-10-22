import type { ArticuloAyuda } from "../../tipos";

export const seguirUnProyecto: ArticuloAyuda = {
  slug: "seguir-un-proyecto",
  moduloSlug: "tareas",
  grupo: "Proyectos",
  titulo: "Cómo seguir y editar un proyecto",
  resumen: "Detalle, edición y cambio de estado del proyecto.",
  introduccion:
    "El detalle de un proyecto muestra sus datos y todas sus tareas. Desde ahí se agregan tareas y, si tu rol gestiona proyectos, se edita el proyecto o se lo finaliza, cancela o reabre.",
  permisos: ["tareas.gestionar"],
  palabrasClave: [
    "proyecto",
    "detalle del proyecto",
    "editar proyecto",
    "finalizar",
    "cancelar proyecto",
    "reabrir",
    "estado del proyecto",
    "abierto",
    "finalizado",
    "cancelado",
    "tareas del proyecto",
  ],
  pasos: [
    {
      titulo: "Abrí el detalle del proyecto",
      etiquetaIndice: "Abrir el detalle",
      cuerpo:
        "En Proyectos (grupo Trabajo del menú) hacé clic en la fila (o en Ver). También llegás desde el nombre del proyecto en un cuadro de Tareas o desde el detalle de una tarea. Los filtros por columna y el orden funcionan como en el listado de tareas; sin orden elegido, los proyectos van agrupados por estado y luego por fecha de inicio.",
      captura: {
        src: "/ayuda/tareas/seguir-un-proyecto-paso-1.webp",
        alt: "Detalle de un proyecto con Datos, Descripción y Tareas.",
      },
    },
    {
      titulo: "Leé los datos y las tareas",
      etiquetaIndice: "Datos y tareas",
      cuerpo:
        "El panel Datos muestra Fecha de inicio, Fecha de fin y Responsable, y debajo ves la Descripción. En Tareas aparecen todas las tareas del proyecto en la misma tabla del listado, con sus filtros y su semáforo. Si no hay ninguna, dice «Este proyecto todavía no tiene tareas asociadas.»",
    },
    {
      titulo: "Agregá una tarea al proyecto",
      etiquetaIndice: "Nueva Tarea",
      cuerpo:
        "Si podés crear tareas y el proyecto está Abierto, el botón Nueva Tarea abre el formulario con el proyecto ya fijado. Un proyecto Finalizado o Cancelado no admite tareas nuevas y oculta el botón.",
    },
    {
      titulo: "Editá el proyecto",
      etiquetaIndice: "Editar",
      cuerpo:
        "Solo quien gestiona proyectos (Decanato, Secretaría Académica o el Administrador de Sistemas) ve el botón Editar. Cambiá nombre, descripción, fechas o responsable y pulsá Guardar cambios. El estado no se cambia desde este formulario.",
      nota: {
        tipo: "info",
        titulo: "Si no ves Editar",
        cuerpo:
          "Tu rol puede ver el proyecto y sus tareas, pero no modificarlo. Con Administración, por ejemplo, el detalle es de solo lectura salvo Nueva Tarea.",
      },
    },
    {
      titulo: "Cambiá el estado del proyecto",
      etiquetaIndice: "Cambiar el estado",
      cuerpo:
        "El estado actual se muestra como etiqueta (Abierto, Finalizado o Cancelado), y junto a ella botones con las acciones posibles: Finalizar, Cancelar o Reabrir. El cambio se aplica al hacer clic, sin pedir confirmación. Un proyecto Cancelado no puede pasar directo a Finalizado: primero hay que reabrirlo.",
      captura: {
        src: "/ayuda/tareas/seguir-un-proyecto-paso-5.webp",
        alt: "Cabecera del proyecto con los botones de estado.",
      },
      nota: {
        tipo: "importante",
        titulo: "Cerrar un proyecto no cierra sus tareas",
        cuerpo:
          "Al finalizar o cancelar un proyecto, sus tareas conservan el estado que tenían. Las autoridades dejan de verlas en un cuadro de Tareas (siguen en el detalle del proyecto) y los responsables las siguen viendo en su tabla. Revisalas antes de cerrar.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Un proyecto Abierto con tareas aparece como cuadro en Tareas. Al reabrirlo, vuelve a admitir tareas nuevas y a tener su cuadro.",
  },
  preguntas: [
    {
      pregunta: "Edité un proyecto y dice «El recurso cambió».",
      respuesta:
        "Otra persona lo modificó mientras lo editabas. Cerrá el formulario, abrí Editar de nuevo para ver la versión actual y volvé a hacer tu cambio.",
    },
    {
      pregunta: "¿Dónde encuentro un proyecto Finalizado?",
      respuesta:
        "En Proyectos (grupo Trabajo del menú): el listado incluye todos los proyectos, sin importar su estado.",
    },
  ],
  relacionados: ["tareas/crear-un-proyecto", "tareas/crear-una-tarea", "tareas/abrir-una-tarea"],
};
