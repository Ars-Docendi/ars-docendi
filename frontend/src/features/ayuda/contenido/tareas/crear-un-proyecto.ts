import type { ArticuloAyuda } from "../../tipos";

export const crearUnProyecto: ArticuloAyuda = {
  slug: "crear-un-proyecto",
  moduloSlug: "tareas",
  grupo: "Proyectos",
  titulo: "Cómo crear un proyecto",
  resumen: "Alta de un proyecto y quién puede hacerlo.",
  introduccion:
    "Un proyecto agrupa tareas que persiguen un mismo objetivo. Aparece como un cuadro propio en Tareas mientras está Abierto. Ver el listado de proyectos y crear uno nuevo son permisos distintos.",
  permisos: ["tareas.gestionar"],
  palabrasClave: [
    "proyecto",
    "nuevo proyecto",
    "crear proyecto",
    "alta",
    "agrupar tareas",
    "responsable del proyecto",
    "decanato",
    "secretaría académica",
    "sin permiso",
  ],
  pasos: [
    {
      titulo: "Abrí Proyectos",
      etiquetaIndice: "Abrir Proyectos",
      cuerpo:
        "En el menú, grupo Trabajo, elegí Proyectos. La tabla lista todos los proyectos, sea cual sea su estado: N°, Nombre, Responsable, Inicio, Fin, Estado y Acciones. Es también donde encontrás los Finalizados y Cancelados.",
      captura: {
        src: "/ayuda/tareas/crear-un-proyecto-paso-1.webp",
        alt: "Listado de proyectos con el botón Nuevo Proyecto.",
      },
      nota: {
        tipo: "importante",
        titulo: "El ítem Proyectos y el permiso para crear",
        cuerpo:
          "El ítem Proyectos aparece para quien puede crear tareas (Secretaría Académica, Decanato y Administración). Pero crear y modificar proyectos lo permite el servidor solo a Decanato, Secretaría Académica y el Administrador de Sistemas. Si sos de Administración vas a ver el listado y los detalles, sin el botón Nuevo Proyecto.",
      },
    },
    {
      titulo: "Pulsá Nuevo Proyecto",
      etiquetaIndice: "Nuevo Proyecto",
      cuerpo:
        "Arriba a la derecha pulsá Nuevo Proyecto. Si no lo ves, tu rol no puede crear proyectos y el servidor rechazaría la operación aunque llegaras al formulario por otro camino.",
    },
    {
      titulo: "Completá el formulario",
      etiquetaIndice: "Formulario",
      cuerpo:
        "El Nombre, la Fecha de inicio, la Fecha de fin y el Responsable son obligatorios; la Descripción es opcional. La fecha de fin tiene que ser igual o posterior a la de inicio. El estado no se elige: el proyecto nace Abierto.",
      captura: {
        src: "/ayuda/tareas/crear-un-proyecto-paso-3.webp",
        alt: "Ventana Nuevo Proyecto completa.",
      },
    },
    {
      titulo: "Elegí al responsable del proyecto",
      etiquetaIndice: "Responsable",
      cuerpo:
        "Pulsá Buscar en Responsable y elegí a la persona en la ventana Buscar Responsable. Para un proyecto solo se ofrecen Decanato y Secretaría Académica, y dentro de tu jerarquía: no podés asignar a alguien de nivel superior al tuyo.",
    },
    {
      titulo: "Creá el proyecto",
      etiquetaIndice: "Crear",
      cuerpo:
        "Pulsá Crear proyecto. Si algo falla, el formulario muestra «No se pudo guardar el proyecto» con el motivo y conserva lo que escribiste.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "El proyecto aparece en el listado y ya se le pueden asociar tareas: al crear una tarea, elegilo en Proyecto, o creala desde el detalle del proyecto. Su cuadro aparece en Tareas cuando tenga al menos una tarea.",
  },
  preguntas: [
    {
      pregunta: "Veo Proyectos en el menú pero no puedo crear uno.",
      respuesta:
        "El ítem se muestra a quienes crean tareas, pero crear proyectos requiere un permiso propio que tienen Decanato, Secretaría Académica y el Administrador de Sistemas. Pedí a una de esas áreas que lo cree.",
    },
    {
      pregunta: "¿Se puede eliminar un proyecto?",
      respuesta:
        "La pantalla no ofrece eliminar proyectos. Un proyecto se Finaliza o se Cancela y puede reabrirse; así sus tareas se conservan.",
    },
  ],
  relacionados: [
    "tareas/seguir-un-proyecto",
    "tareas/crear-una-tarea",
    "primeros-pasos/permisos-y-pantallas-visibles",
  ],
};
