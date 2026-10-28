import type { ArticuloAyuda } from "../../tipos";

export const ingresarAlSistema: ArticuloAyuda = {
  slug: "ingresar-al-sistema",
  moduloSlug: "primeros-pasos",
  grupo: "Empezar",
  titulo: "Cómo ingresar al sistema",
  resumen: "Entrá con tu cuenta institucional de Microsoft y qué hacer si no te deja.",
  introduccion:
    "Ars Docendi usa tu cuenta institucional de Microsoft: no hay usuario ni contraseña propios del sistema. Para entrar, tu cuenta tiene que estar dada de alta por la administración.",
  permisos: [],
  palabrasClave: [
    "login",
    "iniciar sesión",
    "ingresar",
    "entrar",
    "acceso",
    "microsoft",
    "cuenta institucional",
    "contraseña",
    "no puedo entrar",
    "acceso denegado",
    "soporte",
  ],
  pasos: [
    {
      titulo: "Abrí la pantalla de ingreso",
      etiquetaIndice: "Pantalla de ingreso",
      cuerpo:
        "Cuando abrís Ars Docendi sin una sesión iniciada, el sistema te lleva a la pantalla Iniciá sesión. Si habías intentado abrir una pantalla en particular, al ingresar vas a volver a ella; si no, llegás a Mi portal.",
      captura: {
        src: "/ayuda/primeros-pasos/ingresar-al-sistema-paso-1.webp",
        alt: "Pantalla Iniciá sesión en su estado inicial, con el botón «Iniciar sesión con cuenta institucional» y el texto «¿Problemas para ingresar?» visibles.",
      },
    },
    {
      titulo: "Pulsá Iniciar sesión con cuenta institucional",
      etiquetaIndice: "Iniciar sesión",
      cuerpo:
        "El botón dice «Redirigiendo a Microsoft…» mientras el sistema te lleva a la página de Microsoft. Si tenés más de una cuenta abierta en el navegador, Microsoft te pide elegir con cuál querés ingresar.",
    },
    {
      titulo: "Elegí tu cuenta y confirmá",
      etiquetaIndice: "Elegir la cuenta",
      cuerpo:
        "Usá la cuenta cuyo mail dio de alta la administración. Si Microsoft termina bien, volvés al sistema con la sesión iniciada. En el primer ingreso, el sistema vincula tu cuenta de Microsoft con tu usuario.",
      nota: {
        tipo: "importante",
        cuerpo:
          "Ingresar no crea usuarios: solo entran las personas que ya fueron dadas de alta, con la cuenta activa y al menos un rol vigente.",
      },
    },
    {
      titulo: "Si no te deja ingresar",
      etiquetaIndice: "Si no te deja",
      cuerpo:
        "Hay dos avisos posibles sobre el botón. «No pudimos confirmar tu identidad» significa que Microsoft devolvió un error o cancelaste el proceso: probá de nuevo. «No pudimos iniciar tu sesión» significa que tu cuenta no puede acceder al sistema: comunicate con Secretaría Académica. Para otros problemas, escribí a soporte.adocendi@unlam.edu.ar indicando tu legajo o DNI.",
      captura: {
        src: "/ayuda/primeros-pasos/ingresar-al-sistema-paso-4.webp",
        alt: "Pantalla Iniciá sesión con el aviso «No pudimos iniciar tu sesión» (estado de acceso denegado).",
      },
      nota: {
        tipo: "info",
        cuerpo:
          "Por seguridad, el aviso no dice el motivo: puede ser que tu mail no esté registrado, que tu cuenta esté desactivada o que no tengas un rol vigente.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Entrás al sistema y ves el menú lateral con las pantallas que tu rol puede usar. La sesión vence si pasa un tiempo sin actividad o al llegar a su duración máxima; en ese caso, el sistema te devuelve a la pantalla de ingreso y tenés que iniciar sesión de nuevo.",
  },
  preguntas: [
    {
      pregunta: "Mi cuenta de Microsoft funciona, pero el sistema no me deja entrar. ¿Por qué?",
      respuesta:
        "Tener cuenta de Microsoft no alcanza: tu mail tiene que estar dado de alta en Ars Docendi, con la cuenta activa y un rol vigente. Comunicate con Secretaría Académica para que lo verifique.",
    },
    {
      pregunta: "¿Cerrar sesión en Ars Docendi cierra mi sesión de Microsoft?",
      respuesta:
        "No. Cerrar sesión solo termina tu sesión en Ars Docendi; tu sesión de Microsoft en el navegador sigue abierta.",
    },
  ],
  relacionados: [
    "primeros-pasos/recorrer-la-pantalla",
    "primeros-pasos/permisos-y-pantallas-visibles",
  ],
};
