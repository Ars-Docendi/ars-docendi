import type { ArticuloAyuda } from "../../tipos";

export const adjuntarDocumentacion: ArticuloAyuda = {
  slug: "adjuntar-documentacion",
  moduloSlug: "designaciones",
  grupo: "Pedir una designación",
  titulo: "Cómo adjuntar la documentación de un pedido",
  resumen: "Formatos y tamaños admitidos y cómo quitar un adjunto.",
  introduccion:
    "Qué documentos hay que adjuntar depende del tipo de novedad. El sistema no te deja guardar un Alta o una Baja si falta algo obligatorio.",
  permisos: ["designaciones.gestionar"],
  palabrasClave: [
    "adjuntos",
    "documentación",
    "archivo",
    "cv",
    "dni",
    "justificativo",
    "pdf",
    "jpeg",
    "png",
    "tamaño",
    "subir",
    "quitar adjunto",
    "faltan adjuntos",
  ],
  pasos: [
    {
      titulo: "Identificá qué te piden",
      etiquetaIndice: "Qué adjuntar",
      cuerpo:
        "Alta: CV (PDF), DNI · Frente y DNI · Dorso, los tres obligatorios. Baja: un documento justificativo, obligatorio. Cambio de cargo o dedicación: un documento de respaldo, opcional. Cada zona de carga indica su formato y tamaño máximo.",
      captura: {
        src: "/ayuda/designaciones/adjuntar-documentacion-paso-1.webp",
        alt: "Las tres zonas de carga de un Alta, con los textos de formato y tamaño bajo cada una.",
      },
    },
    {
      titulo: "Subí cada archivo",
      etiquetaIndice: "Subir archivos",
      cuerpo:
        "Arrastrá el archivo a la zona correspondiente o hacé clic para elegirlo. El CV y el justificativo son PDF de hasta 10 MiB. Las fotos del DNI son JPEG o PNG de hasta 5 MiB cada una. Si cargás otro archivo en la misma zona, reemplaza al anterior.",
    },
    {
      titulo: "Quitá un adjunto si te equivocaste",
      etiquetaIndice: "Quitar un adjunto",
      cuerpo:
        "Cada archivo cargado tiene una × (Quitar). Al quitarlo, la zona vuelve a quedar vacía y, si el adjunto es obligatorio, el sistema vuelve a exigirlo.",
    },
    {
      titulo: "Guardá y corregí los avisos",
      etiquetaIndice: "Avisos al guardar",
      cuerpo:
        "Si falta algo, el formulario no guarda y muestra el aviso Faltan adjuntos con la lista (por ejemplo, «Falta adjuntar: DNI (dorso)»); en una Baja el aviso es Falta el justificativo. Si un archivo no es del tipo o del tamaño indicado, el sistema lo rechaza al guardar y no crea el pedido.",
      captura: {
        src: "/ayuda/designaciones/adjuntar-documentacion-paso-4.webp",
        alt: "Aviso «Faltan adjuntos» sobre las zonas de carga de un Alta.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los adjuntos viajan con el pedido. Quien lo revisa los abre desde la Documentación adjunta del detalle del pedido, en una pestaña nueva.",
  },
  preguntas: [
    {
      pregunta: "Subí una foto en la zona del CV y el sistema la rechazó. ¿Por qué?",
      respuesta:
        "Cada zona acepta un formato distinto, y el sistema comprueba el contenido real del archivo, no solo su extensión. Revisá que cada archivo esté en la zona que indica su formato.",
    },
  ],
  relacionados: ["designaciones/crear-un-pedido", "designaciones/editar-un-pedido"],
};
