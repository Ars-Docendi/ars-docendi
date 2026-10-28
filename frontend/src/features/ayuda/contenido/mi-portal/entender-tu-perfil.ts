import type { ArticuloAyuda } from "../../tipos";

export const entenderTuPerfil: ArticuloAyuda = {
  slug: "entender-tu-perfil",
  moduloSlug: "mi-portal",
  grupo: "Tu perfil",
  titulo: "Cómo entender tu perfil",
  resumen: "Qué datos son institucionales (solo lectura) y cuáles editás vos.",
  introduccion:
    "Mi portal es tu perfil docente. Se lee tal como está y cada sección se edita por separado, con su propio guardado: no hay un botón de guardar general ni campos obligatorios en toda la página.",
  permisos: ["portal.ver"],
  palabrasClave: [
    "perfil",
    "portal",
    "mi portal",
    "datos personales",
    "legajo",
    "dni",
    "cuil",
    "solo lectura",
    "no puedo editar",
    "datos institucionales",
    "guardar",
  ],
  pasos: [
    {
      titulo: "Abrí Mi portal",
      etiquetaIndice: "Abrir Mi portal",
      cuerpo:
        "En el menú lateral, grupo Personal, elegí Mi portal. Es la pantalla a la que llegás al ingresar al sistema.",
      captura: {
        src: "/ayuda/mi-portal/entender-tu-perfil-paso-1.webp",
        alt: "Mi portal completo, con el encabezado de identidad y las secciones visibles.",
      },
    },
    {
      titulo: "Reconocé tus datos institucionales",
      etiquetaIndice: "Datos institucionales",
      cuerpo:
        "El encabezado muestra tu nombre y tu rol, y debajo el Mail institucional, el Número de DNI, el Legajo y el CUIL. Estas filas no tienen flecha ni botón: son de solo lectura, vienen de la institución y no se editan desde acá. Si alguno es incorrecto, pedí la corrección a Secretaría Académica.",
      captura: {
        src: "/ayuda/mi-portal/entender-tu-perfil-paso-2.webp",
        alt: "Bloque de identidad con las cuatro filas de solo lectura (sin flecha).",
      },
    },
    {
      titulo: "Ubicá las secciones que sí editás",
      etiquetaIndice: "Secciones editables",
      cuerpo:
        "Podés completar Contacto, CV, Experiencia, Educación, Certificaciones, Proyectos, Habilidades e Intereses. Una sección sin datos aparece como una fila con un botón + Agregar; al cargar el primer dato se expande, así que la página crece con tu perfil.",
      captura: {
        src: "/ayuda/mi-portal/entender-tu-perfil-paso-3.webp",
        alt: "Secciones vacías como filas con + Agregar, junto a una sección ya completa.",
      },
    },
    {
      titulo: "Confirmá que se guardó",
      etiquetaIndice: "Confirmar el guardado",
      cuerpo:
        "Cada vez que guardás una sección aparece un aviso Cambios guardados que se cierra solo. Si algo falla, ves una alerta No pudimos guardar los cambios con el motivo. Los datos persisten: al volver a abrir Mi portal los vas a encontrar.",
      captura: {
        src: "/ayuda/mi-portal/entender-tu-perfil-paso-4.webp",
        alt: "Aviso verde «Cambios guardados» en la parte inferior de la pantalla.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Tu perfil queda disponible para las pantallas del sistema que lo usan. Podés volver a editarlo cuando quieras; no hay un cierre ni una fecha límite.",
  },
  preguntas: [
    {
      pregunta:
        "Edité una sección y me apareció «Los datos cambiaron mientras editabas». ¿Qué hago?",
      respuesta:
        "Significa que el mismo dato se modificó desde otra pestaña o sesión mientras lo editabas. El sistema no pisa el valor nuevo. Cerrá el formulario, revisá cómo quedó la lista y volvé a hacer tu cambio.",
    },
    {
      pregunta: "¿Mi legajo o mi DNI están mal, cómo los cambio?",
      respuesta:
        "No se cambian desde Mi portal. Son datos institucionales: escribí a Secretaría Académica para que los corrija.",
    },
  ],
  relacionados: [
    "mi-portal/completar-datos-de-contacto",
    "mi-portal/cargar-educacion-y-certificaciones",
    "mi-portal/subir-tu-cv",
  ],
};
