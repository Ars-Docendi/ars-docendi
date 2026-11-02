import type { ArticuloAyuda } from "../../tipos";

export const recorrerLaPantalla: ArticuloAyuda = {
  slug: "recorrer-la-pantalla",
  moduloSlug: "primeros-pasos",
  grupo: "Moverte por el sistema",
  titulo: "Cómo recorrer la pantalla",
  resumen: "Menú lateral, migas de pan y cierre de sesión.",
  introduccion:
    "Todas las pantallas del sistema comparten la misma estructura: un menú a la izquierda y el contenido de la pantalla a la derecha. En el celular el menú se abre desde el botón ☰ de la barra de arriba.",
  permisos: [],
  palabrasClave: [
    "menú",
    "sidebar",
    "navegación",
    "migas de pan",
    "breadcrumb",
    "inicio",
    "cerrar sesión",
    "salir",
    "usuario",
    "rol",
  ],
  pasos: [
    {
      titulo: "Ubicá tu usuario",
      etiquetaIndice: "Tu usuario",
      cuerpo:
        "Arriba del menú lateral están el botón ☰ y la marca Ars Docendi, que te lleva a Mi portal. Al pie del menú ves tus iniciales, tu nombre y tu rol, que indican con qué usuario estás trabajando.",
    },
    {
      titulo: "Usá el menú lateral",
      etiquetaIndice: "Menú lateral",
      cuerpo:
        "El menú está agrupado en Personal, Trabajo, Designaciones, Sistema y Configuración. Solo aparecen los grupos y las pantallas que tu rol puede usar. La pantalla donde estás queda resaltada. Al pie del menú encontrás el acceso al Centro de ayuda. Con el botón ☰ podés achicar el menú a solo íconos y volver a desplegarlo.",
      captura: {
        src: "/ayuda/primeros-pasos/recorrer-la-pantalla-paso-2.webp",
        alt: "Menú lateral de un usuario con varios grupos, con una pantalla resaltada y el enlace Centro de ayuda al pie.",
      },
    },
    {
      titulo: "Ubicate con las migas de pan",
      etiquetaIndice: "Migas de pan",
      cuerpo:
        "Arriba del título de cada pantalla, una línea como Inicio › Mis pedidos › Detalle del pedido te muestra dónde estás. Cada nivel anterior es un enlace para volver. Inicio te lleva a Mi portal.",
      captura: {
        src: "/ayuda/primeros-pasos/recorrer-la-pantalla-paso-3.webp",
        alt: "Migas de pan de una pantalla de tres niveles, por ejemplo el detalle de un pedido.",
      },
    },
    {
      titulo: "Cerrá la sesión",
      etiquetaIndice: "Cerrar sesión",
      cuerpo:
        "Hacé clic sobre tu nombre y rol, al pie del menú lateral, para abrir el menú de usuario. Elegí Cerrar sesión y el sistema te lleva a la pantalla de ingreso. En el celular, abrí el menú con el botón ☰ y tu usuario aparece al final.",
      nota: {
        tipo: "consejo",
        cuerpo:
          "Cerrá la sesión siempre que uses una computadora compartida. Quedan registradas tus acciones en el sistema.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Al cerrar la sesión, el sistema borra tu sesión del navegador y te pide ingresar de nuevo para volver a usarlo. Tu sesión de Microsoft no se modifica.",
  },
  preguntas: [
    {
      pregunta: "¿Por qué el menú lateral se achica cuando entro al Centro de ayuda?",
      respuesta:
        "Dentro del Centro de ayuda el menú se reduce a íconos para dejar lugar al índice de la documentación. Podés desplegarlo con el botón ☰, y al salir de la ayuda vuelve a mostrarse como lo tenías.",
    },
  ],
  relacionados: [
    "primeros-pasos/permisos-y-pantallas-visibles",
    "primeros-pasos/usar-el-centro-de-ayuda",
  ],
};
