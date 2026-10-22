import type { ArticuloAyuda } from "../../tipos";

export const completarDatosDeContacto: ArticuloAyuda = {
  slug: "completar-datos-de-contacto",
  moduloSlug: "mi-portal",
  grupo: "Tu perfil",
  titulo: "Cómo completar tus datos de contacto",
  resumen: "Cargar y corregir tu teléfono y tu mail de contacto.",
  introduccion:
    "La sección Contacto tiene dos datos que editás vos: Teléfono y Mail. Se muestran siempre, aunque estén vacíos, para que sepas qué podés cargar. Cada uno se edita por separado.",
  permisos: ["portal.ver"],
  palabrasClave: [
    "contacto",
    "teléfono",
    "celular",
    "mail",
    "correo",
    "email",
    "cambiar teléfono",
    "datos de contacto",
    "editar",
  ],
  pasos: [
    {
      titulo: "Ubicá la sección Contacto",
      etiquetaIndice: "Sección Contacto",
      cuerpo:
        "En Mi portal, buscá la sección Contacto. Cada fila muestra el valor cargado o, si falta, el texto Sin cargar, y termina con una flecha que indica que se puede editar.",
      captura: {
        src: "/ayuda/mi-portal/completar-datos-de-contacto-paso-1.webp",
        alt: "Sección Contacto con una fila cargada y otra «Sin cargar».",
      },
    },
    {
      titulo: "Hacé clic en el dato que querés cambiar",
      etiquetaIndice: "Abrir el editor",
      cuerpo:
        "Hacé clic en la fila Teléfono o en la fila Mail. Se abre una ventana con ese único campo. Escribí el valor nuevo; para dejarlo vacío, borrá el contenido.",
      captura: {
        src: "/ayuda/mi-portal/completar-datos-de-contacto-paso-2.webp",
        alt: "Ventana de edición del campo Mail, abierta y con el cursor en el campo.",
      },
    },
    {
      titulo: "Guardá el cambio",
      etiquetaIndice: "Guardar",
      cuerpo:
        "Pulsá Guardar o la tecla Enter. Si el mail no tiene un formato válido, la ventana queda abierta con el aviso debajo del campo para que lo corrijas. Si te arrepentís, pulsá Cancelar y el dato no cambia.",
      captura: {
        src: "/ayuda/mi-portal/completar-datos-de-contacto-paso-3.webp",
        alt: "Ventana de edición del Mail con el mensaje de error de formato debajo del campo.",
      },
      nota: {
        tipo: "consejo",
        cuerpo:
          "Cargá el teléfono con el código de área, por ejemplo 11 4000 0000, para que puedan ubicarte sin dudas.",
      },
    },
    {
      titulo: "Verificá el resultado",
      etiquetaIndice: "Verificar",
      cuerpo:
        "La ventana se cierra, la fila muestra el valor nuevo y aparece el aviso Cambios guardados.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Tu mail de contacto queda en tu perfil y se usa donde el sistema necesita tus datos de contacto, por ejemplo al armar el lote de designaciones. No reemplaza tu Mail institucional, que es el de tu cuenta y no se edita.",
  },
  preguntas: [
    {
      pregunta: "¿Cuál es la diferencia entre Mail y Mail institucional?",
      respuesta:
        "El Mail institucional es el de tu cuenta de la universidad; está arriba, es de solo lectura y no lo cambiás vos. El Mail de la sección Contacto es un dato adicional que podés cargar y corregir cuando quieras.",
    },
  ],
  relacionados: ["mi-portal/entender-tu-perfil", "mi-portal/subir-tu-cv"],
};
