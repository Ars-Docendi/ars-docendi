import type { ArticuloAyuda } from "../../tipos";

export const permisosYPantallasVisibles: ArticuloAyuda = {
  slug: "permisos-y-pantallas-visibles",
  moduloSlug: "primeros-pasos",
  grupo: "Moverte por el sistema",
  titulo: "Por qué ves solo algunas pantallas",
  resumen: "Por qué ves solo algunas pantallas y a quién pedir más acceso.",
  introduccion:
    "Cada pantalla del sistema depende de un permiso que viene de tu rol. Si no tenés el permiso, la pantalla no aparece en tu menú, y tampoco su ayuda.",
  permisos: [],
  palabrasClave: [
    "permisos",
    "rol",
    "acceso",
    "no veo",
    "no aparece",
    "falta una pantalla",
    "pedir acceso",
    "menú",
    "sin permiso",
    "redirige",
  ],
  pasos: [
    {
      titulo: "Fijate qué grupos ves en el menú",
      etiquetaIndice: "Tu menú",
      cuerpo:
        "El menú lateral muestra solo lo que tu rol puede usar. Por ejemplo, Mis pedidos aparece si podés cargar pedidos de designación, Revisión si revisás pedidos y Períodos si administrás los períodos. Dos personas con roles distintos ven menús distintos.",
      captura: {
        src: "/ayuda/primeros-pasos/permisos-y-pantallas-visibles-paso-1.webp",
        alt: "Menús de dos roles distintos lado a lado (por ejemplo un docente y la Secretaría Académica).",
      },
    },
    {
      titulo: "Entendé qué pasa si abrís una dirección sin permiso",
      etiquetaIndice: "Sin permiso",
      cuerpo:
        "Si escribís la dirección de una pantalla para la que no tenés permiso, el sistema no muestra un error: te lleva a Mi portal. El control real lo hace el servidor, así que ocultar o mostrar un enlace nunca cambia lo que podés hacer.",
    },
    {
      titulo: "Pedí el acceso que te falta",
      etiquetaIndice: "Pedir acceso",
      cuerpo:
        "Los permisos se asignan por rol y los administra la institución. Si necesitás una pantalla que no ves, pedí a Secretaría Académica que revise tu rol, indicando qué necesitás hacer.",
    },
    {
      titulo: "Volvé a entrar si te cambiaron el rol",
      etiquetaIndice: "Cambio de rol",
      cuerpo:
        "Cuando te cambian los permisos, el menú se actualiza al revalidar tu sesión. Si una pantalla desapareció o apareció una nueva y no la ves, actualizá la página o cerrá sesión y volvé a ingresar.",
    },
  ],
  despues: {
    titulo: "¿Qué ves en el Centro de ayuda?",
    cuerpo:
      "La ayuda sigue la misma regla que el menú: solo aparecen los módulos y artículos de las pantallas que podés usar. Si te cambian el rol, la ayuda se actualiza igual que el menú.",
  },
  preguntas: [
    {
      pregunta: "Un compañero ve una pantalla que yo no veo. ¿Es un error?",
      respuesta:
        "Normalmente no: ve esa pantalla porque su rol tiene el permiso y el tuyo no. Si creés que deberías tenerlo, pedilo a Secretaría Académica.",
    },
    {
      pregunta: "¿Quién decide qué permisos tiene cada rol?",
      respuesta:
        "Los permisos de cada rol los administra la institución desde la pantalla Roles, que solo ven las personas autorizadas a hacerlo. Para pedir un cambio, hablá con Secretaría Académica.",
    },
  ],
  relacionados: [
    "primeros-pasos/recorrer-la-pantalla",
    "primeros-pasos/usar-el-centro-de-ayuda",
    "administracion/administrar-roles-y-permisos",
  ],
};
