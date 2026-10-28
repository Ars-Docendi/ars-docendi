import type { ArticuloAyuda } from "../../tipos";

export const crearUnaTarea: ArticuloAyuda = {
  slug: "crear-una-tarea",
  moduloSlug: "tareas",
  grupo: "Gestionar tareas",
  titulo: "Cómo crear una tarea",
  resumen: "Alta de una tarea, responsable y vencimiento.",
  introduccion:
    "Crear tareas es propio de las autoridades que coordinan el trabajo del departamento: Secretaría Académica, Decanato y Administración. Quien crea una tarea queda como su autor, la asigna a un responsable y es la única persona que puede editarla o cancelarla.",
  permisos: ["tareas.gestionar"],
  palabrasClave: [
    "crear tarea",
    "nueva tarea",
    "alta",
    "asignar",
    "responsable",
    "prioridad",
    "tipo",
    "vencimiento",
    "fecha de fin",
    "tarea hija",
    "proyecto",
    "jerarquía",
    "editar tarea",
  ],
  pasos: [
    {
      titulo: "Abrí el formulario",
      etiquetaIndice: "Abrir el formulario",
      cuerpo:
        "En Tareas pulsá Nueva Tarea, arriba a la derecha. También podés crearla desde el detalle de un proyecto (queda en ese proyecto) o desde el detalle de otra tarea con Nueva tarea hija (hereda el proyecto de la tarea padre).",
      captura: {
        src: "/ayuda/tareas/crear-una-tarea-paso-1.webp",
        alt: "Pantalla Tareas con el botón Nueva Tarea resaltado.",
      },
    },
    {
      titulo: "Completá el título y la descripción",
      etiquetaIndice: "Título y descripción",
      cuerpo:
        "El Título es obligatorio y conviene que diga qué hay que hacer. La Descripción es opcional y sirve para dar el detalle.",
    },
    {
      titulo: "Fijá las fechas",
      etiquetaIndice: "Fechas",
      cuerpo:
        "La Fecha de inicio y la Fecha de fin son obligatorias. La de fin tiene que ser igual o posterior a la de inicio; si no, el formulario avisa «Debe ser posterior o igual a la Fecha de inicio». Con estas fechas se calcula el semáforo de vencimiento: elegí un plazo realista.",
    },
    {
      titulo: "Elegí prioridad y tipo",
      etiquetaIndice: "Prioridad y tipo",
      cuerpo:
        "Prioridad (Alta, Media o Baja) y Tipo (Extensión, Administrativa, Posgrado, Investigación, Académicas o Decanato) son obligatorios.",
    },
    {
      titulo: "Elegí al responsable",
      etiquetaIndice: "Responsable",
      cuerpo:
        "Pulsá Buscar en el campo Responsable. En la ventana Buscar Responsable podés filtrar por tipo de usuario o escribir un nombre, apellido, legajo o DNI y pulsar Buscar; hacé clic en la persona para elegirla. Con Cambiar podés elegir a otra.",
      captura: {
        src: "/ayuda/tareas/crear-una-tarea-paso-5.webp",
        alt: "Ventana Buscar Responsable con resultados.",
      },
      nota: {
        tipo: "importante",
        titulo: "Solo se puede asignar hacia abajo",
        cuerpo:
          "La jerarquía va de mayor a menor así: Decanato, Secretaría Académica, Administración, Coordinador de Carrera, Jefe de Cátedra, Docente. Podés asignar a alguien de tu mismo nivel o de uno inferior, nunca superior; el buscador solo lista a esas personas, y a las que están activas.",
      },
    },
    {
      titulo: "Asociala a un proyecto (opcional)",
      etiquetaIndice: "Proyecto",
      cuerpo:
        "En Proyecto podés elegir uno de los proyectos Abiertos o dejar Sin proyecto, y la tarea irá al cuadro Generales. En una tarea hija el campo está bloqueado y muestra el proyecto del padre.",
    },
    {
      titulo: "Creá la tarea",
      etiquetaIndice: "Crear",
      cuerpo:
        "Pulsá Crear tarea. Si falta algún dato obligatorio, cada campo muestra «Campo obligatorio». La tarea nace Pendiente, con 0 % de avance y un número correlativo, y aparece en el listado.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "El responsable ve la tarea en su listado y puede cambiar su estado y su avance. Vos, como autor, podés cambiar los datos con Editar en el detalle mientras la tarea exista, y cancelarla con Cancelar.",
    viñetas: [
      {
        termino: "Editar",
        descripcion:
          "Abre el mismo formulario con los datos cargados; el botón es Guardar cambios. Si cambiás el proyecto de una tarea, sus tareas hijas se mueven con ella.",
      },
    ],
  },
  preguntas: [
    {
      pregunta: "El responsable que quiero no aparece en el buscador.",
      respuesta:
        "Puede estar en un nivel superior al tuyo, estar inactivo o no coincidir con el texto buscado. Probá con otro dato (legajo o DNI) y revisá el filtro de tipo de usuario.",
    },
    {
      pregunta: "No puedo asignar una tarea a un proyecto.",
      respuesta:
        "Solo los proyectos Abiertos aceptan tareas nuevas. Si el proyecto está Finalizado o Cancelado, tiene que reabrirlo quien gestiona proyectos.",
    },
    {
      pregunta: "No veo el botón Nueva Tarea.",
      respuesta:
        "Lo ven solo los roles que pueden crear tareas. Si lo necesitás, pedí a Secretaría Académica que revise tu rol.",
    },
  ],
  relacionados: [
    "tareas/cambiar-el-estado-de-una-tarea",
    "tareas/abrir-una-tarea",
    "tareas/seguir-un-proyecto",
  ],
};
