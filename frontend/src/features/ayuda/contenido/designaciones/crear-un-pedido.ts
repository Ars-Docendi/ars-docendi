import type { ArticuloAyuda } from "../../tipos";

export const crearUnPedido: ArticuloAyuda = {
  slug: "crear-un-pedido",
  moduloSlug: "designaciones",
  grupo: "Pedir una designación",
  titulo: "Cómo crear un pedido de designación",
  resumen: "Nuevo pedido paso a paso, según el tipo de novedad.",
  introduccion:
    "Un pedido de designación comunica una novedad sobre un docente en una materia a tu cargo: un Alta, una Baja o un Cambio de cargo o dedicación. Cada pedido cubre una sola materia y se carga dentro del período activo.",
  permisos: ["designaciones.gestionar"],
  palabrasClave: [
    "pedido",
    "designación",
    "nuevo pedido",
    "alta",
    "baja",
    "cambio de cargo",
    "cambio de dedicación",
    "novedad",
    "jefe de cátedra",
    "docente",
    "cargar pedido",
    "período",
    "borrador",
    "enviar",
  ],
  pasos: [
    {
      titulo: "Abrí Nuevo pedido",
      etiquetaIndice: "Abrir Nuevo pedido",
      cuerpo:
        "En el menú, grupo Designaciones, elegí Mis pedidos y pulsá Nuevo pedido, arriba a la derecha. El subtítulo del formulario muestra el período en el que vas a cargar el pedido. Tiene que haber un período activo; si no lo hay, pedile a Secretaría Académica que active uno.",
      captura: {
        src: "/ayuda/designaciones/crear-un-pedido-paso-1.webp",
        alt: "Mis pedidos con el botón Nuevo pedido resaltado.",
      },
    },
    {
      titulo: "Elegí el tipo de novedad",
      etiquetaIndice: "Tipo de novedad",
      cuerpo:
        "En Tipo de novedad elegí Alta (docente nuevo), Baja (se da de baja a un docente) o Cambio de cargo o dedicación (se modifica una designación existente). El resto del formulario se adapta a tu elección.",
      captura: {
        src: "/ayuda/designaciones/crear-un-pedido-paso-2.webp",
        alt: "Formulario recién abierto con las tres opciones de Tipo de novedad.",
      },
    },
    {
      titulo: "Indicá el docente y la materia",
      etiquetaIndice: "Docente y materia",
      cuerpo:
        "En un Alta ingresás DNI, Nombre y Apellido de la persona nueva, y elegís la Materia. En una Baja o un Cambio elegís el Docente de la lista; el sistema completa sus datos actuales (cargo, dedicación, horas, antigüedad), que son de solo lectura, y propone la Materia. Solo podés elegir materias de tu cátedra.",
      captura: {
        src: "/ayuda/designaciones/crear-un-pedido-paso-3.webp",
        alt: "Sección Docente de un Cambio con un docente elegido y el panel de datos actuales.",
      },
      nota: {
        tipo: "importante",
        cuerpo:
          "Un docente solo puede tener un pedido en curso por período, sin importar la cátedra. Si ya existe uno, el sistema avisa «Ya existe un pedido en curso para este docente en el período.» y no te deja cargar otro. Un pedido rechazado o cancelado libera al docente.",
      },
    },
    {
      titulo: "Completá la designación solicitada",
      etiquetaIndice: "Designación solicitada",
      cuerpo:
        "En un Alta o un Cambio, completá Cargo solicitado, Dedicación solicitada y la carga horaria de la materia (debe ser mayor que 0). Las Horas de investigación y las Horas externas (otro depto.) son opcionales. En una Baja esta sección no aparece.",
    },
    {
      titulo: "Escribí la justificación",
      etiquetaIndice: "Justificación",
      cuerpo:
        "En una Baja elegí el Tipo de baja (si es Otro, describilo en Detalle) y completá el Motivo de la baja. En un Cambio, el Motivo del pedido es obligatorio. En un Alta, el Motivo del pedido no es obligatorio, pero conviene completarlo.",
    },
    {
      titulo: "Adjuntá la documentación",
      etiquetaIndice: "Documentación",
      cuerpo:
        "El Alta exige CV y DNI (frente y dorso), la Baja exige un documento justificativo y el Cambio admite un documento de respaldo opcional. Los formatos y tamaños están en la guía de adjuntos.",
      captura: {
        src: "/ayuda/designaciones/crear-un-pedido-paso-6.webp",
        alt: "Sección Documentación obligatoria · Alta con las tres zonas de carga.",
      },
    },
    {
      titulo: "Guardá el borrador o enviá el pedido",
      etiquetaIndice: "Guardar o enviar",
      cuerpo:
        "Pulsá Guardar pedido para dejarlo como borrador y seguir después, o Guardar y enviar para mandarlo a revisión en el mismo paso. Antes de guardar, el formulario marca cada campo o adjunto que falta. Cancelar vuelve a Mis pedidos sin guardar.",
      captura: {
        src: "/ayuda/designaciones/crear-un-pedido-paso-7.webp",
        alt: "Pie del formulario con los botones Cancelar, Guardar pedido y Guardar y enviar.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Volvés a Mis pedidos, donde el pedido aparece como Borrador (si lo guardaste) o En Coordinación (si lo enviaste). Un borrador lo podés editar o eliminar; un pedido enviado queda de solo lectura y sigue la cadena de revisión hasta el lote o hasta una devolución.",
  },
  preguntas: [
    {
      pregunta: "El docente que busco no aparece en la lista de Baja o Cambio. ¿Por qué?",
      respuesta:
        "Puede ser que ya tenga un pedido en curso en el período, o que no tenga una designación vigente en una materia a tu cargo. Solo aparecen docentes sobre los que podés pedir una novedad.",
    },
    {
      pregunta: "¿Por qué no me deja guardar una Baja o un Cambio sobre cierto docente?",
      respuesta:
        "Estas dos novedades operan sobre una designación existente, así que el docente tiene que tener legajo. Si el sistema indica que falta el legajo, hablá con Secretaría Académica.",
    },
  ],
  relacionados: [
    "designaciones/adjuntar-documentacion",
    "designaciones/editar-un-pedido",
    "designaciones/seguir-el-estado-de-un-pedido",
  ],
};
