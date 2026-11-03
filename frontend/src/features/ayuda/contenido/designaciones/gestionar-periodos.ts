import type { ArticuloAyuda } from "../../tipos";

export const gestionarPeriodos: ArticuloAyuda = {
  slug: "gestionar-periodos",
  moduloSlug: "designaciones",
  grupo: "Períodos",
  titulo: "Cómo gestionar los períodos de designación",
  resumen: "Crear, editar, activar, desactivar y eliminar períodos de designación.",
  introduccion:
    "Un período de designación define dos ventanas: la de carga, en la que se admiten pedidos, y la de impacto, en la que rigen las designaciones. A lo sumo un período puede estar activo, y es el que usan Mis pedidos, Revisión y el lote.",
  permisos: ["periodos.administrar"],
  palabrasClave: [
    "período",
    "períodos",
    "cuatrimestre",
    "activar",
    "desactivar",
    "ventana de carga",
    "impacto",
    "nuevo período",
    "eliminar período",
    "período activo",
    "secretaría académica",
  ],
  pasos: [
    {
      titulo: "Abrí Períodos",
      etiquetaIndice: "Abrir Períodos",
      cuerpo:
        "En el menú, grupo Designaciones, elegí Períodos. La tabla muestra Nombre, Carga desde, Carga hasta, Impacto desde, Impacto hasta (solo mes y año), Activo y Acciones. Los encabezados tienen filtros para buscar.",
      captura: {
        src: "/ayuda/designaciones/gestionar-periodos-paso-1.webp",
        alt: "Tabla de Períodos con un período activo y otros inactivos.",
      },
    },
    {
      titulo: "Creá un período",
      etiquetaIndice: "Crear",
      cuerpo:
        "Pulsá Nuevo período. Completá el Nombre, la Ventana para cargar pedidos (Desde y Hasta) y el Período de impacto de la designación (Desde y Hasta). Todos los campos son obligatorios y cada Hasta no puede ser anterior a su Desde. Si dejás vacío el Hasta de la ventana de carga, al elegir el Desde del período de impacto el sistema propone una fecha un mes antes.",
      captura: {
        src: "/ayuda/designaciones/gestionar-periodos-paso-2.webp",
        alt: "Ventana Nuevo período con las dos ventanas de fechas.",
      },
    },
    {
      titulo: "Activá el período",
      etiquetaIndice: "Activar",
      cuerpo:
        "Con el interruptor Período activo lo dejás activo para que se carguen pedidos en él. Solo puede haber un período activo: si ya hay otro, el sistema avisa «Ya existe un período activo» con su nombre y te pide desactivarlo primero. No cambia el período existente.",
      captura: {
        src: "/ayuda/designaciones/gestionar-periodos-paso-3.webp",
        alt: "Aviso «Ya existe un período activo…» en el campo Período activo.",
      },
    },
    {
      titulo: "Editá o desactivá un período",
      etiquetaIndice: "Editar y desactivar",
      cuerpo:
        "Pulsá Editar en la fila, cambiá lo que necesites y Guardar. Para desactivar un período activo, apagá el interruptor Período activo y guardá: aparece la confirmación Desactivar período, que te recuerda que el Jefe de Cátedra ya no podrá cargar pedidos. Confirmá con Desactivar o cancelá. Reactivarlo no pide confirmación.",
      captura: {
        src: "/ayuda/designaciones/gestionar-periodos-paso-4.webp",
        alt: "Ventana de confirmación Desactivar período.",
      },
    },
    {
      titulo: "Eliminá un período que no se usó",
      etiquetaIndice: "Eliminar",
      cuerpo:
        "Pulsá el botón de eliminar de la fila. La confirmación Eliminar período advierte que no se puede deshacer. Si el período ya tiene pedidos asociados, el sistema no lo elimina y lo explica en la misma ventana: «El período tiene pedidos asociados y no se puede eliminar.»",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los cambios se guardan en el momento. Mientras no haya un período activo no se pueden cargar pedidos nuevos ni descargar el lote, así que conviene activar el siguiente antes de desactivar el actual.",
  },
  preguntas: [
    {
      pregunta: "Desactivé el período por error. ¿Se perdieron los pedidos?",
      respuesta:
        "No. Los pedidos quedan asociados al período y no se pierden. Volvé a activarlo desde Editar.",
    },
    {
      pregunta: "¿Para qué sirve el período de impacto?",
      respuesta:
        "Indica desde y hasta cuándo rigen las designaciones del período. El lote usa su nombre y su rango para armar los encabezados del libro.",
    },
  ],
  relacionados: ["designaciones/descargar-el-lote-de-finalizados", "designaciones/crear-un-pedido"],
};
