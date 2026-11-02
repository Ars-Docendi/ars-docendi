import type { ArticuloAyuda } from "../../tipos";

export const cargarExperienciaYProyectos: ArticuloAyuda = {
  slug: "cargar-experiencia-y-proyectos",
  moduloSlug: "mi-portal",
  grupo: "Tu trayectoria",
  titulo: "Cómo cargar tu experiencia y tus proyectos",
  resumen: "Registrar experiencia laboral y proyectos con mes y año.",
  introduccion:
    "En Experiencia cargás los puestos que ocupaste; en Proyectos, los proyectos en los que participaste, con un documento en PDF y un DOI si los tienen. Las dos secciones usan el mismo selector de período.",
  permisos: ["portal.ver"],
  palabrasClave: [
    "experiencia",
    "experiencia laboral",
    "puesto",
    "trabajo",
    "proyecto",
    "investigación",
    "doi",
    "pdf",
    "documento",
    "período",
    "mes",
    "año",
    "en curso",
    "agregar",
    "eliminar",
  ],
  pasos: [
    {
      titulo: "Pulsá + Agregar en la sección",
      etiquetaIndice: "Agregar",
      cuerpo:
        "En Mi portal, en Experiencia o en Proyectos, pulsá + Agregar. Se abre la ventana Agregar experiencia o Agregar proyecto.",
      captura: {
        src: "/ayuda/mi-portal/cargar-experiencia-y-proyectos-paso-1.webp",
        alt: "Sección Experiencia vacía con el botón + Agregar.",
      },
    },
    {
      titulo: "Completá los datos de una experiencia",
      etiquetaIndice: "Datos de la experiencia",
      cuerpo:
        "Completá Puesto, Organización y De qué se trató. Todos son obligatorios. Si seguís en ese puesto, marcá Sigo en este puesto y el campo Hasta se desactiva.",
      captura: {
        src: "/ayuda/mi-portal/cargar-experiencia-y-proyectos-paso-2.webp",
        alt: "Ventana Agregar experiencia completa, con la casilla «Sigo en este puesto».",
      },
    },
    {
      titulo: "Completá los datos de un proyecto",
      etiquetaIndice: "Datos del proyecto",
      cuerpo:
        "Completá Nombre, Tu rol y De qué se trata. Si el proyecto sigue activo, marcá Sigue en curso. Opcionalmente podés cargar un DOI y un Documento: arrastrá un PDF o hacé clic para subirlo.",
      captura: {
        src: "/ayuda/mi-portal/cargar-experiencia-y-proyectos-paso-3.webp",
        alt: "Ventana Agregar proyecto con los campos DOI y Documento visibles.",
      },
    },
    {
      titulo: "Indicá el período con mes y año",
      etiquetaIndice: "Período",
      cuerpo:
        "Desde es obligatorio y Hasta es opcional (se desactiva si el ítem está en curso). Elegí el año y, si querés, el mes; podés elegirlos en cualquier orden. Un mes sin año no se puede guardar y muestra «Completá el año.». La fecha Hasta no puede ser anterior a Desde.",
    },
    {
      titulo: "Guardá y revisá la lista",
      etiquetaIndice: "Guardar",
      cuerpo:
        "Pulsá Guardar. Si falta un dato obligatorio, el campo muestra un aviso y la ventana sigue abierta. Al guardar, el ítem aparece ordenado por período y se muestra el aviso Cambios guardados. La experiencia vigente se destaca en la línea de tiempo.",
      captura: {
        src: "/ayuda/mi-portal/cargar-experiencia-y-proyectos-paso-5.webp",
        alt: "Sección Experiencia con dos ítems en la línea de tiempo, uno vigente.",
      },
    },
    {
      titulo: "Editá o eliminá un ítem",
      etiquetaIndice: "Editar o eliminar",
      cuerpo:
        "Cada ítem tiene un menú de acciones con Editar y Eliminar. Eliminar abre una confirmación con el mensaje «Esta acción no se puede deshacer».",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los cambios se guardan en el momento y quedan en tu perfil. En un proyecto, el DOI y el nombre del documento se muestran como parte del ítem.",
  },
  preguntas: [
    {
      pregunta: "¿Qué formato de archivo acepto en el Documento de un proyecto?",
      respuesta:
        "Solo PDF. El archivo se valida al guardar el proyecto; si no es un PDF válido o supera el tamaño permitido, el sistema rechaza el guardado.",
    },
  ],
  relacionados: [
    "mi-portal/cargar-educacion-y-certificaciones",
    "mi-portal/elegir-habilidades-e-intereses",
    "mi-portal/subir-tu-cv",
  ],
};
