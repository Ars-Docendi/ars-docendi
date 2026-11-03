import type { ArticuloAyuda } from "../../tipos";

export const resolverUnPedido: ArticuloAyuda = {
  slug: "resolver-un-pedido",
  moduloSlug: "designaciones",
  grupo: "Revisar pedidos",
  titulo: "Cómo resolver un pedido",
  resumen: "Aprobar, rechazar, devolver o priorizar un pedido, y qué efecto tiene cada acción.",
  introduccion:
    "La cadena de revisión es Coordinador, Secretaría y Decanato, en ese orden. Cada persona actúa solo cuando el pedido está en su etapa y dentro de su ámbito.",
  permisos: ["designaciones.revisar"],
  palabrasClave: [
    "aprobar",
    "aceptar",
    "rechazar",
    "devolver",
    "devolución",
    "prioritario",
    "prioridad",
    "justificativo",
    "comentario",
    "resolver",
    "revisión",
    "administración",
    "coordinador",
    "decanato",
  ],
  pasos: [
    {
      titulo: "Abrí el pedido desde Revisión",
      etiquetaIndice: "Abrir el pedido",
      cuerpo:
        "En Revisión, hacé clic en el pedido. En el detalle, la cadena de aprobación te muestra en qué etapa está, y el resumen y la documentación adjunta te permiten revisarlo. Los adjuntos se abren en una pestaña nueva.",
      captura: {
        src: "/ayuda/designaciones/resolver-un-pedido-paso-1.webp",
        alt: "Detalle de un pedido en revisión con el panel Acciones de revisión a la derecha.",
      },
    },
    {
      titulo: "Conocé el panel Acciones de revisión",
      etiquetaIndice: "Panel de acciones",
      cuerpo:
        "A la derecha están las acciones que podés hacer: un botón de aprobar con el nombre de la etapa siguiente (Aprobar y pasar a Secretaría, Aprobar y pasar a Decanato o Aprobar y enviar a lote), Rechazar, Devolver y Marcar prioritario. El panel te recuerda con qué rol revisás, y que solo podés actuar en tu etapa y tu ámbito.",
    },
    {
      titulo: "Aprobá el pedido",
      etiquetaIndice: "Aprobar",
      cuerpo:
        "Pulsá el botón de aprobar. En la ventana Aceptar pedido podés dejar un comentario opcional para la próxima etapa. Confirmá con Aprobar y enviar: el pedido pasa a la etapa siguiente o, si lo aprueba Decanato, queda En lote.",
      captura: {
        src: "/ayuda/designaciones/resolver-un-pedido-paso-3.webp",
        alt: "Ventana Aceptar pedido con el comentario opcional.",
      },
      nota: {
        tipo: "importante",
        cuerpo:
          "Administración revisa pero no aprueba: puede rechazar y devolver, pero no ve el botón de aprobar.",
      },
    },
    {
      titulo: "Rechazá el pedido con un motivo",
      etiquetaIndice: "Rechazar",
      cuerpo:
        "Pulsá Rechazar. El Justificativo es obligatorio: sin texto, el botón de confirmar queda deshabilitado. Confirmá con Rechazar novedad. El pedido pasa a Rechazado y el circuito termina para él.",
      captura: {
        src: "/ayuda/designaciones/resolver-un-pedido-paso-4.webp",
        alt: "Ventana Rechazar pedido con el campo Justificativo marcado como obligatorio.",
      },
      nota: {
        tipo: "importante",
        cuerpo:
          "Un rechazo es definitivo: el pedido no admite ninguna acción posterior. Para insistir, el Jefe de Cátedra tiene que cargar un pedido nuevo.",
      },
    },
    {
      titulo: "Devolvé el pedido para que se corrija",
      etiquetaIndice: "Devolver",
      cuerpo:
        "Pulsá Devolver y escribí el Justificativo, obligatorio, con lo que hay que corregir. El pedido pasa a Devuelto y retrocede un nivel: desde Coordinación vuelve al Jefe de Cátedra, desde Secretaría al Coordinador y desde Decanato a Secretaría. Quien lo corrige lo reenvía y el pedido retoma la etapa que lo devolvió.",
      captura: {
        src: "/ayuda/designaciones/resolver-un-pedido-paso-5.webp",
        alt: "Ventana Devolver pedido con el subtítulo que indica a quién vuelve.",
      },
    },
    {
      titulo: "Marcá o quitá la prioridad",
      etiquetaIndice: "Prioridad",
      cuerpo:
        "Cualquier revisor, dentro de su ámbito, puede pulsar Marcar prioritario para avisar la urgencia a los demás niveles; el Justificativo es obligatorio. Esto no cambia el estado del pedido. Para sacar la marca, pulsá Quitar prioritario: ahí el comentario es opcional.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "El Historial del pedido registra la acción con tu nombre, tu comentario y la fecha, y la cadena de aprobación se actualiza. Un pedido prioritario se destaca con una marca y sube al principio del tablero.",
    viñetas: [
      { termino: "Aprobar", descripcion: "Avanza una etapa; Decanato lo deja En lote." },
      { termino: "Rechazar", descripcion: "Termina el circuito; no se puede reabrir." },
      {
        termino: "Devolver",
        descripcion: "Vuelve un nivel para corregir y, al reenviarse, retoma tu etapa.",
      },
    ],
  },
  preguntas: [
    {
      pregunta: "No veo los botones para aprobar, rechazar o devolver. ¿Por qué?",
      respuesta:
        "Solo aparecen cuando el pedido está en tu etapa y dentro de tu ámbito. Si el pedido está en otra etapa, o en la carrera de otro Coordinador, solo podés mirarlo (y, dentro de tu ámbito, marcarlo como prioritario).",
    },
    {
      pregunta: "¿Puedo deshacer una aprobación?",
      respuesta:
        "No. Si detectás un error en un pedido que ya aprobaste, avisá a la persona que lo tiene en la etapa siguiente para que lo devuelva con un comentario.",
    },
  ],
  relacionados: [
    "designaciones/revisar-el-tablero",
    "designaciones/seguir-el-estado-de-un-pedido",
    "designaciones/descargar-el-lote-de-finalizados",
  ],
};
