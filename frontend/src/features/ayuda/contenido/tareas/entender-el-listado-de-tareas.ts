import type { ArticuloAyuda } from "../../tipos";

export const entenderElListadoDeTareas: ArticuloAyuda = {
  slug: "entender-el-listado-de-tareas",
  moduloSlug: "tareas",
  grupo: "Seguir tareas",
  titulo: "Cómo leer el listado de tareas",
  resumen: "Cuadros por proyecto, semáforo de vencimiento y tareas en pausa.",
  introduccion:
    "Tareas reúne el seguimiento interno del departamento. Lo que ves depende de tu rol: las autoridades que crean tareas ven todas, agrupadas en cuadros por proyecto; el resto ve una sola tabla con las tareas que tiene asignadas.",
  permisos: ["tareas.ver"],
  palabrasClave: [
    "tareas",
    "listado",
    "cuadros",
    "generales",
    "proyecto",
    "semáforo",
    "vencimiento",
    "vencida",
    "por vencer",
    "pausa",
    "pendientes",
    "terminadas",
    "mis tareas",
    "tablero",
  ],
  pasos: [
    {
      titulo: "Abrí Tareas",
      etiquetaIndice: "Abrir Tareas",
      cuerpo:
        "En el menú, grupo Trabajo, elegí Tareas. El título indica cuántas tareas hay en total («3 tareas»); ese número cuenta todas las que podés ver, aunque los filtros muestren menos filas. Si todavía no hay ninguna, el sistema lo avisa con «Todavía no hay tareas cargadas».",
      captura: {
        src: "/ayuda/tareas/entender-el-listado-de-tareas-paso-1.webp",
        alt: "Pantalla Tareas con los cuadros Generales y de un proyecto.",
      },
    },
    {
      titulo: "Reconocé los cuadros por proyecto",
      etiquetaIndice: "Cuadros",
      cuerpo:
        "Si podés crear tareas (Secretaría Académica, Decanato o Administración), la pantalla se divide en cuadros. Primero va Generales, con las tareas que no pertenecen a ningún proyecto; después, un cuadro por cada proyecto Abierto que tenga tareas, ordenados por la fecha de fin del proyecto, de la más lejana a la más cercana. El nombre del proyecto es un enlace a su detalle. Los proyectos Finalizados o Cancelados no tienen cuadro: se consultan desde Proyectos.",
      nota: {
        tipo: "info",
        titulo: "Si no creás tareas",
        cuerpo:
          "Con otros roles (por ejemplo Docente, Jefe de Cátedra o Coordinador) ves una única tabla con las tareas que te asignaron, sin cuadros, y con una columna Proyecto que muestra a cuál pertenece cada una. El resto de las tareas del departamento no es visible para vos.",
      },
    },
    {
      titulo: "Elegí qué estados ver",
      etiquetaIndice: "Estado y alcance",
      cuerpo:
        "Arriba de los cuadros, el selector Estado abre en Pendientes: muestra las tareas Pendiente, En curso y Pausa. Terminadas muestra las Resuelta y Cancelada, y Todas no filtra. El selector Tareas (solo para quien crea tareas) pasa de Todas a Propias: las que creaste más las que tenés asignadas. Se combinan, por ejemplo Pendientes y Propias.",
      captura: {
        src: "/ayuda/tareas/entender-el-listado-de-tareas-paso-3.webp",
        alt: "Selectores Estado y Tareas sobre los cuadros.",
      },
    },
    {
      titulo: "Interpretá el semáforo",
      etiquetaIndice: "Semáforo",
      cuerpo:
        "Las tareas Pendiente, En curso y Pausa se colorean según cuánto del plazo transcurrió entre la fecha de inicio y la de fin: menos de 50 % no se resalta (verde), entre 50 % y 80 % la fila se ve amarilla y desde 80 % se ve roja, incluso si ya venció. Las tareas Resuelta y Cancelada no muestran semáforo. En el detalle, el semáforo acompaña a la fecha de fin.",
      captura: {
        src: "/ayuda/tareas/entender-el-listado-de-tareas-paso-4.webp",
        alt: "Filas amarilla y roja en un cuadro.",
      },
      nota: {
        tipo: "importante",
        titulo: "El rojo no siempre significa «vencida»",
        cuerpo:
          "El rojo marca que se consumió el 80 % o más del plazo; una tarea puede estar en rojo con la fecha de fin todavía por delante. Una tarea cuya fecha de inicio y de fin son el mismo día también se ve roja. Mirá siempre la columna Fin.",
      },
    },
    {
      titulo: "Distinguí las tareas en Pausa",
      etiquetaIndice: "Pausa",
      cuerpo:
        "El estado Pausa tiene su propio color (alerta) en la columna Estado. Significa que el responsable frenó la tarea porque necesita consultar algo; el motivo queda en los comentarios de la tarea. Abrila para leerlo.",
    },
    {
      titulo: "Abrí una tarea",
      etiquetaIndice: "Abrir una tarea",
      cuerpo:
        "Hacé clic en la fila o en Ver para ir al detalle. Las columnas son N°, Título, Autor, Responsable, Inicio, Fin, Prioridad, % Avance, Estado y Acciones.",
    },
  ],
  despues: {
    titulo: "¿Qué sigue?",
    cuerpo:
      "Para encontrar una tarea puntual, filtrá y ordená las columnas. Para trabajarla, abrí su detalle.",
  },
  preguntas: [
    {
      pregunta: "No veo una tarea que sé que existe.",
      respuesta:
        "Si no creás tareas, solo ves las que tenés asignadas. Si sí las creás, revisá el selector Estado (abre en Pendientes) y el selector Tareas (Todas o Propias). Una tarea de un proyecto Finalizado o Cancelado no aparece en un cuadro propio.",
    },
    {
      pregunta: "¿Por qué el contador dice más tareas que las que veo?",
      respuesta:
        "El número del título cuenta todas las tareas que podés ver; los selectores y los filtros solo achican las filas de las tablas.",
    },
  ],
  relacionados: [
    "tareas/filtrar-y-ordenar-tareas",
    "tareas/abrir-una-tarea",
    "tareas/crear-una-tarea",
  ],
};
