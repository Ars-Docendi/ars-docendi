import type { ArticuloAyuda, ContenidoAyuda, ModuloAyuda } from "../tipos";

/**
 * Contenido ficticio solo para tests. El registro de producción
 * (`contenido/index.ts`) nunca incluye artículos inventados.
 */
export const MODULOS: readonly ModuloAyuda[] = [
  {
    slug: "general",
    titulo: "General de prueba",
    descripcion: "Módulo abierto a todos.",
    grupos: ["Inicio", "Avanzado"],
    permisos: [],
  },
  {
    slug: "tareas",
    titulo: "Tareas de prueba",
    descripcion: "Módulo de tareas.",
    grupos: ["Seguir", "Gestionar"],
    permisos: ["tareas.ver", "tareas.gestionar"],
  },
  {
    slug: "designaciones",
    titulo: "Designaciones de prueba",
    descripcion: "Módulo de designaciones.",
    grupos: ["Revisión", "Períodos"],
    permisos: ["designaciones.revisar", "periodos.administrar"],
  },
  {
    slug: "administracion",
    titulo: "Administración de prueba",
    descripcion: "Módulo de administración.",
    grupos: ["Roles"],
    permisos: ["roles.ver"],
  },
];

export const ARTICULOS: readonly ArticuloAyuda[] = [
  {
    slug: "bienvenida",
    moduloSlug: "general",
    grupo: "Inicio",
    titulo: "Bienvenida al sistema",
    resumen: "Primer recorrido de prueba.",
    introduccion: "Introducción de la bienvenida.",
    permisos: [],
    pasos: [
      {
        titulo: "Ingresá",
        etiquetaIndice: "Ingreso",
        cuerpo: "Cuerpo del primer paso.",
        captura: {
          src: "/ayuda/general/bienvenida-paso-1.webp",
          alt: "Pantalla de ingreso",
          leyenda: "Pantalla de ingreso al sistema",
        },
      },
      {
        titulo: "Recorré el menú",
        cuerpo: "Cuerpo del segundo paso.",
        nota: { tipo: "importante", titulo: "Atención", cuerpo: "Texto de la nota importante." },
      },
      { titulo: "Cerrá la sesión", cuerpo: "Cuerpo del tercer paso." },
    ],
    despues: {
      titulo: "¿Qué sucede después?",
      cuerpo: "Después queda todo listo.",
      viñetas: [{ termino: "Listo", descripcion: "Ya podés trabajar." }],
    },
    preguntas: [{ pregunta: "¿Puedo cambiar de cuenta?", respuesta: "Sí, cerrando la sesión." }],
    relacionados: ["tareas/crear-tarea", "administracion/gestionar-roles"],
  },
  {
    slug: "atajos",
    moduloSlug: "general",
    grupo: "Avanzado",
    titulo: "Atajos de teclado",
    resumen: "Moverte más rápido.",
    introduccion: "Introducción de los atajos.",
    permisos: [],
    pasos: [{ titulo: "Probá el teclado", cuerpo: "Cuerpo del único paso." }],
    relacionados: [],
  },
  {
    slug: "ver-tareas",
    moduloSlug: "tareas",
    grupo: "Seguir",
    titulo: "Ver tus tareas",
    resumen: "Listado de tareas.",
    introduccion: "Introducción del listado.",
    permisos: ["tareas.ver"],
    palabrasClave: ["pendientes"],
    pasos: [{ titulo: "Abrí el listado", cuerpo: "Cuerpo del paso del listado." }],
    relacionados: [],
  },
  {
    slug: "crear-tarea",
    moduloSlug: "tareas",
    grupo: "Gestionar",
    titulo: "Crear una tarea",
    resumen: "Alta de tareas.",
    introduccion: "Introducción del alta.",
    permisos: ["tareas.gestionar"],
    pasos: [{ titulo: "Completá el formulario", cuerpo: "Cuerpo del paso del alta." }],
    relacionados: [],
  },
  {
    slug: "revisar-tablero",
    moduloSlug: "designaciones",
    grupo: "Revisión",
    titulo: "Revisar el tablero",
    resumen: "Tablero de revisión de pedidos.",
    introduccion: "Introducción del tablero.",
    permisos: ["designaciones.revisar"],
    pasos: [{ titulo: "Abrí la revisión", cuerpo: "Cuerpo del paso del tablero." }],
    relacionados: [],
  },
  {
    slug: "gestionar-periodos",
    moduloSlug: "designaciones",
    grupo: "Períodos",
    titulo: "Períodos de designación",
    resumen: "Alta y edición de períodos.",
    introduccion: "Introducción de los períodos.",
    permisos: ["periodos.administrar"],
    pasos: [{ titulo: "Creá un período", cuerpo: "Cuerpo del paso de períodos." }],
    relacionados: [],
  },
  {
    slug: "gestionar-roles",
    moduloSlug: "administracion",
    grupo: "Roles",
    titulo: "Administrar roles",
    resumen: "Roles y permisos.",
    introduccion: "Introducción de roles.",
    permisos: ["roles.ver"],
    pasos: [{ titulo: "Abrí Roles", cuerpo: "Asigná permisos granulares al rol." }],
    relacionados: [],
  },
];

export const CONTENIDO: ContenidoAyuda = { modulos: MODULOS, articulos: ARTICULOS };
