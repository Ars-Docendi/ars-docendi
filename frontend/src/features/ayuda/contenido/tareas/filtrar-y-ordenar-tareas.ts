import type { ArticuloAyuda } from "../../tipos";

export const filtrarYOrdenarTareas: ArticuloAyuda = {
  slug: "filtrar-y-ordenar-tareas",
  moduloSlug: "tareas",
  grupo: "Seguir tareas",
  titulo: "Cómo filtrar y ordenar las tareas",
  resumen: "Filtros por columna, alcance Todas/Propias y orden.",
  introduccion:
    "Cada tabla de tareas tiene un filtro en el encabezado de cada columna y se puede ordenar con un clic. Los filtros se aplican sobre las tareas que ya podés ver y no recargan la página.",
  permisos: ["tareas.ver"],
  palabrasClave: [
    "filtrar",
    "ordenar",
    "buscar tarea",
    "filtro",
    "columna",
    "responsable",
    "prioridad",
    "estado",
    "propias",
    "todas",
    "encabezado",
  ],
  pasos: [
    {
      titulo: "Empezá por el estado y el alcance",
      etiquetaIndice: "Estado y alcance",
      cuerpo:
        "El selector Estado (Pendientes, Terminadas, Todas) carga de una vez el filtro de la columna Estado. Si podés crear tareas, el selector Tareas te deja ver Todas o solo las Propias (las que creaste o tenés asignadas).",
    },
    {
      titulo: "Filtrá desde el encabezado de una columna",
      etiquetaIndice: "Filtros por columna",
      cuerpo:
        "Pulsá el ícono de filtro junto al nombre de la columna. N°, Título, Inicio y Fin se filtran escribiendo texto (sin distinguir mayúsculas ni acentos; las fechas se buscan como dd/mm/aaaa). Autor, Responsable, Prioridad y Estado se filtran tildando casillas, y se pueden elegir varias a la vez. % Avance busca un valor exacto, por ejemplo 40.",
      captura: {
        src: "/ayuda/tareas/filtrar-y-ordenar-tareas-paso-2.webp",
        alt: "Menú de filtro de la columna Estado con casillas.",
      },
    },
    {
      titulo: "Limpiá un filtro",
      etiquetaIndice: "Limpiar",
      cuerpo:
        "El ícono de la columna muestra un punto mientras el filtro está activo. Para sacarlo, abrí el menú y pulsá Limpiar filtro. Si ningún resultado coincide, la tabla muestra «Sin tareas que cumplan los filtros.»",
      nota: {
        tipo: "consejo",
        cuerpo:
          "Si una tarea «desapareció», revisá si quedó un filtro activo en alguna columna, sobre todo en Estado: el selector lo precarga con Pendientes.",
      },
    },
    {
      titulo: "Ordená con el nombre de la columna",
      etiquetaIndice: "Ordenar",
      cuerpo:
        "Hacé clic en el nombre de la columna: una vez ordena de menor a mayor, otra de mayor a menor y una tercera quita el orden. Sin orden elegido, las tareas se muestran por fecha de inicio, de la más antigua a la más reciente. Prioridad ordena Baja, Media, Alta, y Estado sigue el ciclo Pendiente, En curso, Pausa, Resuelta, Cancelada.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los filtros y el orden son solo de tu pantalla: se pierden al recargar y no cambian nada para otras personas. Cada cuadro de proyecto tiene sus propios filtros y orden.",
  },
  preguntas: [
    {
      pregunta: "¿Puedo guardar mis filtros?",
      respuesta:
        "No. Se reinician al volver a abrir Tareas, que siempre empieza en Pendientes y en Todas.",
    },
  ],
  relacionados: ["tareas/entender-el-listado-de-tareas", "tareas/abrir-una-tarea"],
};
