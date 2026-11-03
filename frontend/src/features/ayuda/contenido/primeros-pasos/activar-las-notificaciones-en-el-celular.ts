import type { ArticuloAyuda } from "../../tipos";

export const activarLasNotificacionesEnElCelular: ArticuloAyuda = {
  slug: "activar-las-notificaciones-en-el-celular",
  moduloSlug: "primeros-pasos",
  grupo: "Moverte por el sistema",
  titulo: "Activar las notificaciones en el celular",
  resumen: "Recibí los avisos en tu iPhone o Android y elegí cuáles.",
  introduccion:
    "Además de verlos en la campana, podés recibir los avisos como una notificación del celular: un aula asignada, un pedido devuelto o una tarea nueva te llegan aunque no tengas el sistema abierto. Se activan una vez por dispositivo, desde las preferencias de notificaciones.",
  permisos: [],
  palabrasClave: [
    "notificaciones",
    "push",
    "celular",
    "iphone",
    "ios",
    "android",
    "avisos",
    "pantalla de inicio",
    "instalar",
    "permitir",
    "bloqueadas",
    "preferencias",
    "no molestar",
  ],
  pasos: [
    {
      titulo: "iPhone o iPad: abrí el sistema en Safari",
      etiquetaIndice: "iPhone: abrir en Safari",
      cuerpo:
        "En iPhone y iPad las notificaciones funcionan solo con iOS o iPadOS 16.4 o más nuevo. Abrí el sistema en Safari e ingresá la dirección de Ars Docendi. Si tu equipo es más viejo, actualizalo desde Ajustes › General › Actualización de software.",
    },
    {
      titulo: "iPhone o iPad: agregá la app a la pantalla de inicio",
      etiquetaIndice: "iPhone: pantalla de inicio",
      cuerpo:
        "Tocá el botón Compartir (el cuadrado con una flecha hacia arriba), elegí Agregar a pantalla de inicio y confirmá con Agregar. Aparece un ícono de Ars Docendi entre tus apps. Es obligatorio: en una pestaña común de Safari, iOS no permite recibir notificaciones.",
      nota: {
        tipo: "importante",
        cuerpo:
          "Desde ahora usá siempre ese ícono para abrir el sistema. Las notificaciones solo funcionan si lo abrís así.",
      },
    },
    {
      titulo: "iPhone o iPad: abrí la app desde el ícono e ingresá",
      etiquetaIndice: "iPhone: abrir e ingresar",
      cuerpo:
        "Abrí Ars Docendi desde el ícono de la pantalla de inicio e ingresá con tu cuenta de Microsoft institucional, como siempre. La sesión de la app es independiente de la de Safari, así que tenés que ingresar una vez más.",
    },
    {
      titulo: "Android: abrí el sistema en Chrome",
      etiquetaIndice: "Android: abrir en Chrome",
      cuerpo:
        "Abrí el sistema en Chrome e ingresá. No hace falta instalar nada, pero si querés tener un ícono propio, tocá el menú ⋮ de Chrome y elegí Instalar app (o Agregar a la pantalla principal).",
    },
    {
      titulo: "Abrí las preferencias de notificaciones",
      etiquetaIndice: "Abrir preferencias",
      cuerpo:
        "Tocá la campana de Notificaciones (en el celular está en la barra de arriba) y, en el panel, el ícono de engranaje. Se abre Preferencias de notificaciones. Si ves la invitación «Recibí los avisos en este dispositivo», tocá Activar.",
    },
    {
      titulo: "Activá las notificaciones en este dispositivo y permitilas",
      etiquetaIndice: "Activar y permitir",
      cuerpo:
        "Encendé el interruptor Notificaciones en este dispositivo. El celular te pregunta si Ars Docendi puede enviarte notificaciones: tocá Permitir. El texto debajo del interruptor cambia a Activas. Para cada celular o navegador donde quieras recibir avisos tenés que repetir este paso.",
    },
    {
      titulo: "Elegí de qué módulos querés recibir avisos",
      etiquetaIndice: "Elegir los módulos",
      cuerpo:
        "Debajo del interruptor del dispositivo hay un interruptor por módulo: reserva de aulas, designaciones y tareas. Por defecto están todos encendidos. Apagá el módulo del que no quieras recibir avisos en el celular, por ejemplo Tareas pero no Reserva de aulas. Solo se muestran los módulos que tu rol puede usar.",
      nota: {
        tipo: "info",
        cuerpo:
          "Apagar un módulo solo silencia sus avisos en el celular: en la campana los seguís viendo siempre.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Cada aviso llega como una notificación con un título corto y lo que pasó, por ejemplo «Aula asignada: Aula 204». Al tocarla se abre el sistema directamente en la solicitud, el pedido o la tarea, y el aviso queda leído. Para dejar de recibirlas:",
    viñetas: [
      {
        termino: "Desde el sistema",
        descripcion:
          "Abrí las preferencias y apagá Notificaciones en este dispositivo (o solo los módulos que no querés).",
      },
      {
        termino: "iPhone o iPad",
        descripcion: "Ajustes › Notificaciones › Ars Docendi, y apagá Permitir notificaciones.",
      },
      {
        termino: "Android",
        descripcion:
          "En Chrome: menú ⋮ › Configuración › Configuración de sitios › Notificaciones, elegí el sitio de Ars Docendi y bloquealo. Con la app instalada: mantené apretado el ícono › Información de la app › Notificaciones.",
      },
    ],
  },
  preguntas: [
    {
      pregunta: "Dice «Bloqueadas en el navegador». ¿Cómo las desbloqueo?",
      respuesta:
        "Significa que, en algún momento, se tocó No permitir. Hay que habilitarlas desde el celular, porque el sistema no puede cambiarlo. En iPhone: Ajustes › Notificaciones › Ars Docendi › Permitir notificaciones. En Android con Chrome: tocá el candado junto a la dirección › Permisos › Notificaciones › Permitir. Después volvé a abrir las preferencias y activá el interruptor.",
    },
    {
      pregunta: "En el iPhone no me aparece la opción para activarlas.",
      respuesta:
        "Revisá tres cosas: que el equipo tenga iOS o iPadOS 16.4 o más nuevo, que hayas agregado la app a la pantalla de inicio desde Safari y que la abras desde ese ícono, no desde Safari. Si abrís el sistema en una pestaña de Safari, las preferencias te lo explican y no dejan activar.",
    },
    {
      pregunta: "Las activé y no me llega nada.",
      respuesta:
        "Probá en este orden: que el interruptor del dispositivo diga Activas; que el aviso en cuestión esté encendido en la lista; que el celular no esté en modo Concentración, No molestar o ahorro de batería; que las notificaciones de la app estén permitidas en los ajustes del celular; y que el aviso no lo hayas generado vos, porque el sistema no te avisa de lo que hacés vos mismo. Si el aviso aparece en la campana pero no en el celular, apagá el interruptor del dispositivo, volvé a encenderlo y permití de nuevo.",
    },
    {
      pregunta: "¿Funciona con la app cerrada, con el celular apagado o sin internet?",
      respuesta:
        "Con la app cerrada, sí: llega igual. Con el celular apagado o sin conexión, el aviso espera hasta un día y se entrega cuando el celular vuelve a conectarse; pasado ese tiempo ya no se envía, pero lo seguís viendo en la campana.",
    },
    {
      pregunta: "Uso el modo Concentración o No molestar. ¿Qué pasa?",
      respuesta:
        "Esos modos silencian las notificaciones de todas las apps. Los avisos igual quedan guardados en el centro de notificaciones del celular y en la campana del sistema. Si querés recibir Ars Docendi con el modo activado, agregá la app a las excepciones del modo.",
    },
    {
      pregunta: "Compartimos el celular o cambio de usuario. ¿A quién le llegan los avisos?",
      respuesta:
        "A quien tenga la sesión abierta. Al cerrar sesión, el celular deja de recibir los avisos de esa cuenta; al ingresar otra persona, el dispositivo pasa a ser de ella. Cada persona elige sus propios tipos de avisos.",
    },
    {
      pregunta: "¿Quién puede leer los avisos que llegan al celular?",
      respuesta:
        "Los avisos viajan cifrados hasta tu celular: Apple, Google, Microsoft o Mozilla, que entregan la notificación, no pueden leer su contenido. Aun así, el celular puede mostrarlos en la pantalla bloqueada; podés ocultar la vista previa desde sus ajustes de notificaciones.",
    },
  ],
  relacionados: ["primeros-pasos/recorrer-la-pantalla", "primeros-pasos/usar-el-centro-de-ayuda"],
};
