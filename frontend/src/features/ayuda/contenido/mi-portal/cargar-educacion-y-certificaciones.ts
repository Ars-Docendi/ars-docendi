import type { ArticuloAyuda } from "../../tipos";

export const cargarEducacionYCertificaciones: ArticuloAyuda = {
  slug: "cargar-educacion-y-certificaciones",
  moduloSlug: "mi-portal",
  grupo: "Tu trayectoria",
  titulo: "Cómo cargar tu educación y tus certificaciones",
  resumen: "Sumar títulos, estudios y certificaciones con su período.",
  introduccion:
    "En Educación cargás tus títulos y estudios; en Certificaciones, cursos y acreditaciones con su fecha y, si corresponde, su vencimiento. Las dos secciones funcionan igual: agregar, editar y eliminar.",
  permisos: ["portal.ver"],
  palabrasClave: [
    "educación",
    "formación",
    "título",
    "grado",
    "maestría",
    "doctorado",
    "especialización",
    "certificación",
    "certificado",
    "curso",
    "vencimiento",
    "estudios",
    "agregar",
    "eliminar",
  ],
  pasos: [
    {
      titulo: "Pulsá + Agregar en la sección",
      etiquetaIndice: "Agregar",
      cuerpo:
        "En Mi portal, en la sección Educación o Certificaciones, pulsá + Agregar. Se abre la ventana Agregar formación o Agregar certificación.",
      captura: {
        src: "/ayuda/mi-portal/cargar-educacion-y-certificaciones-paso-1.webp",
        alt: "Sección Educación vacía con el botón + Agregar.",
      },
    },
    {
      titulo: "Completá los datos de una formación",
      etiquetaIndice: "Datos de la formación",
      cuerpo:
        "Elegí el Nivel (Grado, Especialización, Maestría o Doctorado) y completá Carrera o título e Institución. Después indicá el período: Desde y, si ya terminó, Hasta. El mes es opcional pero, si lo elegís, tenés que completar también el año. Si seguís cursando, marcá Estoy cursando y el campo Hasta se desactiva.",
      captura: {
        src: "/ayuda/mi-portal/cargar-educacion-y-certificaciones-paso-2.webp",
        alt: "Ventana Agregar formación con todos los campos y la casilla «Estoy cursando».",
      },
    },
    {
      titulo: "Completá los datos de una certificación",
      etiquetaIndice: "Datos de la certificación",
      cuerpo:
        "Completá Nombre, Emisor y Fecha. El Vencimiento es opcional: cargalo solo si la certificación caduca. La fecha de vencimiento no puede ser anterior a la fecha de la certificación.",
      captura: {
        src: "/ayuda/mi-portal/cargar-educacion-y-certificaciones-paso-3.webp",
        alt: "Ventana Agregar certificación con los cuatro campos.",
      },
    },
    {
      titulo: "Guardá y revisá la lista",
      etiquetaIndice: "Guardar",
      cuerpo:
        "Pulsá Guardar. Si falta un dato obligatorio (marcado con *), el campo muestra un aviso y la ventana sigue abierta. Al guardar, el ítem aparece en la lista y ves el aviso Cambios guardados. La lista se ordena por período en Educación y por fecha en Certificaciones.",
    },
    {
      titulo: "Mirá el semáforo de vencimiento",
      etiquetaIndice: "Vencimientos",
      cuerpo:
        "Las certificaciones con vencimiento muestran un indicador: verde con Vigente hasta, amarillo con Vence el cuando faltan 90 días o menos, y rojo con Vencida el cuando ya caducó.",
      captura: {
        src: "/ayuda/mi-portal/cargar-educacion-y-certificaciones-paso-5.webp",
        alt: "Tres certificaciones con el indicador verde, amarillo y rojo.",
      },
    },
    {
      titulo: "Editá o eliminá un ítem",
      etiquetaIndice: "Editar o eliminar",
      cuerpo:
        "Cada ítem tiene un menú de acciones con Editar y Eliminar. Editar abre la misma ventana con los datos cargados. Eliminar pide confirmación con el mensaje «Esta acción no se puede deshacer»; confirmá con Eliminar o cancelá.",
      captura: {
        src: "/ayuda/mi-portal/cargar-educacion-y-certificaciones-paso-6.webp",
        alt: "Menú de acciones de un ítem desplegado y diálogo «Eliminar certificación».",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Cada alta, edición o baja se guarda en el momento y queda en tu perfil. No hay un botón de guardado general para estas secciones.",
  },
  preguntas: [
    {
      pregunta: "Elegí un mes pero no me deja guardar. ¿Por qué?",
      respuesta:
        "El mes es opcional, pero si lo elegís hace falta el año. El campo muestra «Completá el año.» hasta que lo cargues. Podés dejar solo el año, sin mes.",
    },
  ],
  relacionados: [
    "mi-portal/cargar-experiencia-y-proyectos",
    "mi-portal/elegir-habilidades-e-intereses",
    "mi-portal/entender-tu-perfil",
  ],
};
