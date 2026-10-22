import type { ArticuloAyuda } from "../../tipos";

export const elegirHabilidadesEIntereses: ArticuloAyuda = {
  slug: "elegir-habilidades-e-intereses",
  moduloSlug: "mi-portal",
  grupo: "Tu trayectoria",
  titulo: "Cómo elegir tus habilidades e intereses",
  resumen: "Elegir del vocabulario o sugerir un término nuevo.",
  introduccion:
    "Habilidades e Intereses son dos listas independientes que comparten el mismo vocabulario de términos. Quitar un término de una lista no lo quita de la otra.",
  permisos: ["portal.ver"],
  palabrasClave: [
    "habilidades",
    "intereses",
    "etiquetas",
    "tags",
    "términos",
    "vocabulario",
    "sugerir",
    "sugerido",
    "competencias",
    "áreas",
  ],
  pasos: [
    {
      titulo: "Activá la edición",
      etiquetaIndice: "Editar",
      cuerpo:
        "En Mi portal, en la sección Habilidades o Intereses, pulsá + Agregar (si está vacía) o Editar. La sección muestra las herramientas para sumar y quitar términos, y el botón cambia a Listo.",
      captura: {
        src: "/ayuda/mi-portal/elegir-habilidades-e-intereses-paso-1.webp",
        alt: "Sección Habilidades en modo edición con el selector y el campo de sugerencia.",
      },
    },
    {
      titulo: "Elegí un término del vocabulario",
      etiquetaIndice: "Elegir un término",
      cuerpo:
        "Abrí el selector Elegí un término… y elegí uno de la lista. Se agrega al instante a tu perfil; no hace falta confirmar. Los términos que ya tenés no se repiten en la lista.",
    },
    {
      titulo: "Sugerí un término nuevo",
      etiquetaIndice: "Sugerir un término",
      cuerpo:
        "Si no encontrás el que buscás, escribilo en el campo Sugerir uno nuevo y pulsá Sugerir (o Enter). El término se agrega a tu lista con la marca Sugerido. Si el sistema ya tiene un término equivalente, lo reutiliza en vez de duplicarlo.",
      captura: {
        src: "/ayuda/mi-portal/elegir-habilidades-e-intereses-paso-3.webp",
        alt: "Término recién sugerido con la marca «Sugerido».",
      },
    },
    {
      titulo: "Quitá un término",
      etiquetaIndice: "Quitar",
      cuerpo:
        "Con la edición activa, cada término tiene una × (Quitar). Hacé clic para sacarlo de esa lista.",
    },
    {
      titulo: "Terminá con Listo",
      etiquetaIndice: "Listo",
      cuerpo:
        "Cada alta o baja se guarda en el momento y muestra el aviso Cambios guardados. Listo solo cierra el modo edición; no guarda nada adicional.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Tus términos quedan en tu perfil, separados por lista. Los términos que sugeriste pasan a formar parte del vocabulario.",
  },
  preguntas: [
    {
      pregunta: "¿Por qué mis términos dicen «Sugerido»?",
      respuesta:
        "Esa marca indica que el término se incorporó por sugerencia de una persona y no figuraba en el vocabulario original. No afecta cómo se usa.",
    },
    {
      pregunta: "Quité una habilidad. ¿Se quitó también de Intereses?",
      respuesta:
        "No. Las dos listas son independientes: un mismo término puede estar en una, en la otra o en ambas.",
    },
  ],
  relacionados: [
    "mi-portal/cargar-experiencia-y-proyectos",
    "mi-portal/cargar-educacion-y-certificaciones",
  ],
};
