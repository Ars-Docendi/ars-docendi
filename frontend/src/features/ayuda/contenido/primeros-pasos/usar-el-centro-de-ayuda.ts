import type { ArticuloAyuda } from "../../tipos";

export const usarElCentroDeAyuda: ArticuloAyuda = {
  slug: "usar-el-centro-de-ayuda",
  moduloSlug: "primeros-pasos",
  grupo: "Moverte por el sistema",
  titulo: "Cómo usar el Centro de ayuda",
  resumen: "Buscar, leer los pasos y abrir las capturas.",
  introduccion:
    "El Centro de ayuda reúne guías paso a paso de las pantallas que podés usar. Está disponible para todas las personas que ingresaron al sistema.",
  permisos: [],
  palabrasClave: [
    "ayuda",
    "centro de ayuda",
    "manual",
    "guía",
    "buscar",
    "búsqueda",
    "tutorial",
    "capturas",
    "pasos",
    "documentación",
  ],
  pasos: [
    {
      titulo: "Abrí el Centro de ayuda",
      etiquetaIndice: "Abrir la ayuda",
      cuerpo:
        "Hacé clic en Centro de ayuda, al pie del menú lateral. El menú se reduce a íconos y aparece el índice de la documentación. La pantalla de inicio lista los módulos disponibles para vos.",
      captura: {
        src: "/ayuda/primeros-pasos/usar-el-centro-de-ayuda-paso-1.webp",
        alt: "Centro de ayuda abierto, con el menú lateral contraído, el índice de documentación y la lista de módulos.",
      },
    },
    {
      titulo: "Elegí un módulo y un artículo",
      etiquetaIndice: "Elegir un artículo",
      cuerpo:
        "En el índice de la izquierda, cada módulo se despliega con sus artículos. Al abrir un artículo, el índice muestra debajo la lista de sus pasos, y el paso que estás leyendo queda resaltado.",
    },
    {
      titulo: "Buscá por palabra",
      etiquetaIndice: "Buscar",
      cuerpo:
        "Escribí en el campo Buscar… del índice, por ejemplo «CV» o «devolver». Aparecen hasta cinco resultados: con las flechas arriba y abajo recorrés la lista, Enter abre el artículo elegido y Esc cierra el panel. Si pulsás Enter sin elegir, ves todos los resultados en una página. La × borra la búsqueda.",
      captura: {
        src: "/ayuda/primeros-pasos/usar-el-centro-de-ayuda-paso-3.webp",
        alt: "Panel de resultados abierto con una búsqueda de ejemplo y el enlace Ver todos los resultados.",
      },
    },
    {
      titulo: "Seguí los pasos",
      etiquetaIndice: "Leer los pasos",
      cuerpo:
        "Cada artículo tiene pasos numerados. Hacé clic en un paso del índice para ir directo a él. Algunos pasos traen una nota, que puede ser informativa, un consejo o un aviso importante.",
    },
    {
      titulo: "Ampliá las capturas",
      etiquetaIndice: "Ampliar capturas",
      cuerpo:
        "Hacé clic en una captura para verla en pantalla grande. Se cierra con Esc, con el botón de cerrar o haciendo clic fuera de la imagen.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Al final de cada artículo hay una lista de artículos relacionados para seguir con el tema. Para volver a trabajar, elegí cualquier pantalla del menú lateral: sale de la ayuda y el menú recupera su tamaño completo.",
  },
  preguntas: [
    {
      pregunta: "Busco una guía y no aparece. ¿Por qué?",
      respuesta:
        "La ayuda solo muestra los artículos de las pantallas que tu rol puede usar. Revisá también la ortografía o probá con otra palabra relacionada.",
    },
  ],
  relacionados: [
    "primeros-pasos/permisos-y-pantallas-visibles",
    "primeros-pasos/recorrer-la-pantalla",
  ],
};
