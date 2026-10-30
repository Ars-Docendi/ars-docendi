import type { ArticuloAyuda } from "../../tipos";

export const editarUnPedido: ArticuloAyuda = {
  slug: "editar-un-pedido",
  moduloSlug: "designaciones",
  grupo: "Pedir una designación",
  titulo: "Cómo editar un pedido",
  resumen: "Qué se puede cambiar de un pedido en borrador o devuelto.",
  introduccion:
    "Solo podés editar un pedido mientras es un borrador o cuando una revisión te lo devolvió. Una vez enviado, el pedido queda de solo lectura hasta que lo devuelvan.",
  permisos: ["designaciones.gestionar"],
  palabrasClave: [
    "editar",
    "modificar",
    "corregir",
    "borrador",
    "devuelto",
    "reenviar",
    "eliminar pedido",
    "borrar",
    "cambiar un pedido",
    "devolución",
  ],
  pasos: [
    {
      titulo: "Abrí el pedido que querés corregir",
      etiquetaIndice: "Abrir el pedido",
      cuerpo:
        "En Mis pedidos, pulsá Editar en la fila del pedido, o abrí el pedido y pulsá Editar en el encabezado del detalle. El botón solo aparece cuando el pedido se puede editar.",
      captura: {
        src: "/ayuda/designaciones/editar-un-pedido-paso-1.webp",
        alt: "Fila de un borrador en Mis pedidos con los botones Editar y eliminar visibles.",
      },
    },
    {
      titulo: "Modificá los datos",
      etiquetaIndice: "Modificar",
      cuerpo:
        "El formulario se abre con los datos cargados (el subtítulo dice «Editás un borrador» o «Editás un pedido devuelto»). Podés cambiar el docente, la materia, la designación solicitada, la justificación y los adjuntos, con las mismas reglas que al crearlo.",
    },
    {
      titulo: "Atendé la devolución",
      etiquetaIndice: "Pedido devuelto",
      cuerpo:
        "Si el pedido te fue devuelto, arriba del formulario ves el aviso «Te devolvieron este pedido» con el motivo. Quienes tienen el permiso de seguimiento ven además qué rol lo devolvió y a qué etapa vuelve cuando lo reenvíen.",
      captura: {
        src: "/ayuda/designaciones/editar-un-pedido-paso-3.webp",
        alt: "Formulario de un pedido devuelto con el aviso «Devuelto por…» y el comentario.",
      },
    },
    {
      titulo: "Guardá o reenviá",
      etiquetaIndice: "Guardar o reenviar",
      cuerpo:
        "Guardar pedido conserva los cambios sin enviar. Guardar y enviar manda un borrador a revisión; en un pedido devuelto el botón dice Guardar y reenviar y el pedido retoma la etapa que lo devolvió, sin empezar de nuevo la cadena.",
    },
    {
      titulo: "Eliminá un borrador que ya no necesitás",
      etiquetaIndice: "Eliminar un borrador",
      cuerpo:
        "Los borradores tienen una acción de eliminar, en la fila de Mis pedidos y en el detalle. Se abre la confirmación Eliminar pedido con el aviso «Esta acción no se puede deshacer»; confirmá con Eliminar o cancelá.",
      captura: {
        src: "/ayuda/designaciones/editar-un-pedido-paso-5.webp",
        alt: "Diálogo de confirmación «Eliminar pedido».",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Un borrador guardado sigue en Borrador. Un pedido enviado o reenviado pasa a la etapa de revisión correspondiente y vuelve a quedar de solo lectura. El historial del pedido registra quién lo editó y cuándo, y lo ven quienes tienen el permiso de seguimiento.",
  },
  preguntas: [
    {
      pregunta: "Ya envié un pedido y me equivoqué. ¿Puedo editarlo?",
      respuesta:
        "No: tras enviarlo, el pedido queda de solo lectura para que quien revisa evalúe lo que recibió. Solo vuelve a tus manos si una revisión lo devuelve. Pedile a quien lo revisa que lo devuelva con un comentario.",
    },
  ],
  relacionados: [
    "designaciones/crear-un-pedido",
    "designaciones/seguir-el-estado-de-un-pedido",
    "designaciones/resolver-un-pedido",
  ],
};
