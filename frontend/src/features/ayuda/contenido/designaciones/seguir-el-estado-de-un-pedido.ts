import type { ArticuloAyuda } from "../../tipos";

export const seguirElEstadoDeUnPedido: ArticuloAyuda = {
  slug: "seguir-el-estado-de-un-pedido",
  moduloSlug: "designaciones",
  grupo: "Pedir una designación",
  titulo: "Cómo seguir el estado de un pedido",
  resumen: "Mis pedidos, detalle, cadena de revisión y estados.",
  introduccion:
    "Mis pedidos lista los pedidos que cargaste en el período activo. Desde cada fila abrís el detalle. Qué tanto del recorrido ves depende de tu rol: el Jefe de Cátedra ve si el pedido está en proceso, devuelto a él o aprobado; las revisiones ven además la etapa, el historial y quién lo tiene.",
  permisos: ["designaciones.gestionar", "designaciones.revisar"],
  palabrasClave: [
    "estado",
    "seguimiento",
    "mis pedidos",
    "detalle",
    "historial",
    "cadena de aprobación",
    "en revisión",
    "devuelto",
    "rechazado",
    "aprobado",
    "en lote",
    "en proceso",
    "filtrar",
    "ordenar",
    "dónde está mi pedido",
  ],
  pasos: [
    {
      titulo: "Abrí Mis pedidos",
      etiquetaIndice: "Abrir Mis pedidos",
      cuerpo:
        "En el menú, grupo Designaciones, elegí Mis pedidos. El título indica cuántos pedidos hay y el período. Las columnas son Docente, Legajo, Cátedra, Tipo, Enviado, Estado y Acciones.",
      captura: {
        src: "/ayuda/designaciones/seguir-el-estado-de-un-pedido-paso-1.webp",
        alt: "Mis pedidos con varios pedidos en distintos estados.",
      },
    },
    {
      titulo: "Leé el estado de cada pedido",
      etiquetaIndice: "Estados",
      cuerpo:
        "Borrador: todavía no lo enviaste. En proceso: está circulando por el circuito de revisión (como Jefe de Cátedra no ves en qué etapa). Quienes revisan ven en cambio En Coordinación, En Secretaría o En Decanato. Devuelto: te lo devolvieron para que lo corrijas, con el motivo. Aprobado: pasó todas las etapas y quedó en lote. Rechazado: terminó sin aprobarse. Cancelado: el Jefe de Cátedra lo canceló antes de enviarlo y no sigue el circuito.",
    },
    {
      titulo: "Filtrá y ordená la lista",
      etiquetaIndice: "Filtrar y ordenar",
      cuerpo:
        "Cada encabezado tiene un filtro: texto para Docente, Legajo, Cátedra y Enviado, y casillas para Tipo y Estado (Aprobado agrupa los pedidos en lote; En revisión, los que están en proceso). Hacé clic en el nombre de la columna para ordenar: una vez ascendente, otra descendente y una tercera para quitar el orden. Si nada coincide, ves «Sin resultados».",
      captura: {
        src: "/ayuda/designaciones/seguir-el-estado-de-un-pedido-paso-3.webp",
        alt: "Encabezado de Estado con el filtro desplegado y las casillas marcadas.",
      },
    },
    {
      titulo: "Abrí el detalle",
      etiquetaIndice: "Abrir el detalle",
      cuerpo:
        "Hacé clic en la fila (o enfocala y pulsá Enter o Espacio). El detalle muestra el resumen del pedido con su documentación adjunta y, a la derecha, los Datos del trámite. Si te lo devolvieron, arriba ves el motivo. Quienes tienen el permiso de seguimiento ven además la cadena de aprobación y el Historial del pedido.",
      captura: {
        src: "/ayuda/designaciones/seguir-el-estado-de-un-pedido-paso-4.webp",
        alt: "Detalle de un pedido con la cadena de aprobación completa y el historial.",
      },
    },
    {
      titulo: "Interpretá la cadena de aprobación",
      etiquetaIndice: "Cadena de aprobación",
      cuerpo:
        "Con el permiso de seguimiento (Coordinación, Secretaría, Decanato y Administración) ves que el pedido recorre cinco etapas: Jefe de Cátedra, Coordinador, Secretaría, Decanato y En lote, cada una marcada como cumplida, actual, devuelta o rechazada. El Historial del pedido registra cada acción con su autor, comentario y fecha.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Cuando el último revisor aprueba, el pedido pasa a En lote y queda listo para el lote de finalizados que descargan Secretaría, Decanato y Administración. Si lo devuelven, aparece como Devuelto y podés corregirlo y reenviarlo; si lo rechazan, el circuito termina para ese pedido.",
    viñetas: [
      {
        termino: "Devuelto",
        descripcion:
          "Volvés a ser responsable: corregí el pedido y reenviá. Retoma la etapa que lo devolvió.",
      },
      {
        termino: "Rechazado",
        descripcion:
          "Es definitivo. Si querés insistir, cargá un pedido nuevo para el docente (el rechazo libera al docente en el período).",
      },
    ],
  },
  preguntas: [
    {
      pregunta: "¿Por qué mi pedido figura como Aprobado en la lista y En lote en el detalle?",
      respuesta:
        "Son dos nombres para el mismo estado: el pedido terminó la cadena de aprobación y espera ser procesado en el lote.",
    },
    {
      pregunta: "¿Por qué no veo pedidos de otras cátedras?",
      respuesta:
        "Cada persona ve los pedidos de su ámbito. Un Jefe de Cátedra ve los de sus materias; un Coordinador, los de su carrera.",
    },
  ],
  relacionados: [
    "designaciones/editar-un-pedido",
    "designaciones/resolver-un-pedido",
    "designaciones/revisar-el-tablero",
  ],
};
