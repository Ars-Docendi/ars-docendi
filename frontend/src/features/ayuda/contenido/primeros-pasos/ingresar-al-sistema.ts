import type { ArticuloAyuda } from "../../tipos";

export const ingresarAlSistema: ArticuloAyuda = {
  slug: "ingresar-al-sistema",
  moduloSlug: "primeros-pasos",
  grupo: "Empezar",
  titulo: "Cómo ingresar al sistema",
  resumen: "Entrá con tu cuenta institucional @docentes.unlam.edu.ar y qué hacer si no te deja.",
  introduccion:
    "Ars Docendi usa tu cuenta institucional @docentes.unlam.edu.ar, que es una cuenta de Microsoft: no hay usuario ni contraseña propios del sistema. Para entrar, tu cuenta tiene que estar dada de alta por la administración.",
  permisos: [],
  palabrasClave: [
    "login",
    "iniciar sesión",
    "elegir rol",
    "ingresar",
    "entrar",
    "acceso",
    "microsoft",
    "cuenta institucional",
    "contraseña",
    "no puedo entrar",
    "acceso denegado",
    "docentes.unlam.edu.ar",
    "términos y condiciones",
  ],
  pasos: [
    {
      titulo: "Abrí la pantalla de ingreso",
      etiquetaIndice: "Pantalla de ingreso",
      cuerpo:
        "Cuando abrís Ars Docendi sin una sesión iniciada, el sistema te lleva a la pantalla de ingreso, con el formulario «Ingresá a tu cuenta». Si habías intentado abrir una pantalla en particular, al ingresar vas a volver a ella; si no, llegás a Mi portal.",
      captura: {
        src: "/ayuda/primeros-pasos/ingresar-al-sistema-paso-1.webp",
        alt: "Pantalla de ingreso en su estado inicial: a la izquierda, la bienvenida a Ars Docendi; a la derecha, el formulario «Ingresá a tu cuenta» con el botón «Ingresar con Microsoft».",
      },
    },
    {
      titulo: "Pulsá Ingresar con Microsoft",
      etiquetaIndice: "Ingresar con Microsoft",
      cuerpo:
        "El botón dice «Redirigiendo a Microsoft…» mientras el sistema te lleva a la página de Microsoft. Si tenés más de una cuenta abierta en el navegador, Microsoft te pide elegir con cuál querés ingresar.",
    },
    {
      titulo: "Elegí tu cuenta y confirmá",
      etiquetaIndice: "Elegir la cuenta",
      cuerpo:
        "Usá tu cuenta @docentes.unlam.edu.ar, la que dio de alta la administración. Si Microsoft termina bien, volvés al sistema con la sesión iniciada. En el primer ingreso, el sistema vincula tu cuenta de Microsoft con tu usuario.",
      nota: {
        tipo: "importante",
        cuerpo:
          "Ingresar no crea usuarios: solo entran las personas que ya fueron dadas de alta, con la cuenta activa y al menos un rol vigente.",
      },
    },
    {
      titulo: "Elegí con qué rol ingresar",
      etiquetaIndice: "Elegir el rol",
      cuerpo:
        "Si tenés más de un rol, o el mismo rol en distintas materias o carreras, entrás con el primero y se abre «¿Con qué rol querés trabajar?», con una opción por cada uno y su materia o carrera. Elegí uno, o cerrá la ventana para seguir con el marcado. Trabajás siempre con un solo rol a la vez: ves sus pantallas y solo lo de esa materia o carrera. Si tenés un único rol, entrás directo. La pregunta se repite en cada ingreso.",
      captura: {
        src: "/ayuda/primeros-pasos/ingresar-al-sistema-paso-4.webp",
        alt: "Ventana «¿Con qué rol querés trabajar?» con dos roles y el primero marcado.",
      },
    },
    {
      titulo: "Si no te deja ingresar",
      etiquetaIndice: "Si no te deja",
      cuerpo:
        "Hay dos avisos posibles sobre el botón. «No pudimos confirmar tu identidad» significa que Microsoft devolvió un error o cancelaste el proceso: probá de nuevo. «No pudimos iniciar tu sesión» significa que tu cuenta no puede acceder al sistema: comunicate con Secretaría Académica. Si el error de Microsoft se repite, también comunicate con Secretaría Académica.",
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
      "Entrás al sistema y ves el menú lateral con las pantallas que el rol elegido puede usar. Para pasar a otro rol sin salir, usá Cambiar rol en el menú de usuario. La sesión vence si pasa un tiempo sin actividad o al llegar a su duración máxima; en ese caso, el sistema te devuelve a la pantalla de ingreso y tenés que iniciar sesión de nuevo.",
  },
  preguntas: [
    {
      pregunta: "¿Qué acepto al ingresar?",
      respuesta:
        "Al ingresar aceptás los Términos de uso y la Política de privacidad, enlazados al pie de la pantalla de ingreso. Ahí se explica qué datos guarda el sistema, para qué se usan y cómo ejercer tus derechos sobre ellos.",
    },
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
