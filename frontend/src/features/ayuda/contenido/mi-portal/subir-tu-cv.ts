import type { ArticuloAyuda } from "../../tipos";

export const subirTuCv: ArticuloAyuda = {
  slug: "subir-tu-cv",
  moduloSlug: "mi-portal",
  grupo: "Tu CV",
  titulo: "Cómo subir tu CV",
  resumen: "Subir, reemplazar o quitar tu CV en PDF.",
  introduccion:
    "Tu perfil guarda un único CV en formato PDF. No hay historial: al subir uno nuevo reemplaza al anterior.",
  permisos: ["portal.ver"],
  palabrasClave: [
    "cv",
    "currículum",
    "curriculum",
    "pdf",
    "subir archivo",
    "adjuntar",
    "reemplazar",
    "quitar",
    "eliminar cv",
    "arrastrar",
  ],
  pasos: [
    {
      titulo: "Ubicá la sección CV",
      etiquetaIndice: "Sección CV",
      cuerpo:
        "En Mi portal, la sección CV muestra una zona de arrastre con el texto «Arrastrá tu CV en PDF o hacé clic para subirlo».",
      captura: {
        src: "/ayuda/mi-portal/subir-tu-cv-paso-1.webp",
        alt: "Sección CV vacía con la zona de arrastre.",
      },
    },
    {
      titulo: "Subí tu archivo",
      etiquetaIndice: "Subir el PDF",
      cuerpo:
        "Arrastrá el PDF a la zona o hacé clic para elegirlo desde tu equipo. Si el archivo no es un PDF, aparece la alerta No se pudo cargar el CV con el aviso «El CV tiene que ser un archivo PDF.» y no se sube.",
    },
    {
      titulo: "Esperá la validación",
      etiquetaIndice: "Validación",
      cuerpo:
        "Al subirlo, el sistema comprueba que el contenido realmente sea un PDF, que no supere el tamaño máximo de 10 MiB y que no tenga virus. Si algo falla, ves una alerta con el motivo y se conserva tu CV anterior.",
    },
    {
      titulo: "Confirmá que quedó cargado",
      etiquetaIndice: "Confirmar",
      cuerpo:
        "Cuando termina, la zona muestra el nombre del archivo, su tamaño y la fecha de la última actualización.",
      captura: {
        src: "/ayuda/mi-portal/subir-tu-cv-paso-4.webp",
        alt: "Sección CV con un archivo cargado (nombre, tamaño y fecha de actualización).",
      },
    },
    {
      titulo: "Reemplazá o quitá el CV",
      etiquetaIndice: "Reemplazar o quitar",
      cuerpo:
        "Para reemplazarlo, subí otro PDF: el perfil conserva solo el nuevo. Para quitarlo, usá la × (Quitar) del archivo.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Tu CV queda asociado a tu perfil, guardado en un almacenamiento privado. Al reemplazarlo, el archivo anterior deja de estar vinculado a tu perfil.",
  },
  preguntas: [
    {
      pregunta: "Mi archivo termina en .pdf pero el sistema lo rechaza. ¿Por qué?",
      respuesta:
        "El sistema verifica el contenido real del archivo, no solo la extensión. Un archivo renombrado, dañado o de otro formato se rechaza. Volvé a exportar tu CV como PDF desde el programa original.",
    },
  ],
  relacionados: ["mi-portal/entender-tu-perfil", "mi-portal/completar-datos-de-contacto"],
};
