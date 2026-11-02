import type { ArticuloAyuda } from "../../tipos";

export const cambiarElEstadoDeUnaTarea: ArticuloAyuda = {
  slug: "cambiar-el-estado-de-una-tarea",
  moduloSlug: "tareas",
  grupo: "Seguir tareas",
  titulo: "Cómo cambiar el estado de una tarea",
  resumen: "Pendiente, En curso, Pausa (con motivo) y Resuelta (con solución).",
  introduccion:
    "Una tarea pasa por cinco estados: Pendiente, En curso, Pausa, Resuelta y Cancelada. La persona responsable mueve la tarea entre los primeros cuatro y actualiza su avance; cancelarla o reabrirla una vez terminada le corresponde a la autoridad que la creó.",
  permisos: ["tareas.ver"],
  palabrasClave: [
    "estado",
    "cambiar estado",
    "pendiente",
    "en curso",
    "pausa",
    "resuelta",
    "cancelada",
    "cancelar tarea",
    "reabrir",
    "solución",
    "motivo",
    "avance",
    "porcentaje",
    "responsable",
  ],
  pasos: [
    {
      titulo: "Abrí la tarea y buscá el panel Acciones",
      etiquetaIndice: "Panel Acciones",
      cuerpo:
        "En el detalle de la tarea, a la derecha, el panel Acciones muestra los botones de Cambiar estado que te corresponden: nunca el estado actual. Si no sos responsable ni autoridad creadora de la tarea, el panel no aparece.",
      captura: {
        src: "/ayuda/tareas/cambiar-el-estado-de-una-tarea-paso-1.webp",
        alt: "Panel Acciones con botones de estado y campo de avance.",
      },
    },
    {
      titulo: "Pasá a Pendiente o En curso",
      etiquetaIndice: "Pendiente y En curso",
      cuerpo:
        "Pulsá Pendiente o En curso: el cambio se aplica al instante y queda en el Historial. Podés ir de un estado abierto a otro sin orden fijo.",
    },
    {
      titulo: "Poné la tarea en Pausa con un motivo",
      etiquetaIndice: "Pausa",
      cuerpo:
        "Pulsá Pausa. Se abre el campo Motivo de la consulta («Contá qué necesitás consultar antes de seguir…»), que es obligatorio. Escribilo y confirmá con Confirmar Pausa; el botón queda deshabilitado mientras esté vacío o solo tenga espacios. El motivo se guarda como comentario de la tarea.",
      captura: {
        src: "/ayuda/tareas/cambiar-el-estado-de-una-tarea-paso-3.webp",
        alt: "Campo Motivo de la consulta con el botón Confirmar Pausa.",
      },
    },
    {
      titulo: "Resolvé la tarea con una solución",
      etiquetaIndice: "Resuelta",
      cuerpo:
        "Pulsá Resuelta. En el campo Solución («Contá cómo se resolvió la tarea…») explicá cómo se cerró y confirmá con Confirmar Resuelta. La solución es obligatoria y aparece luego en su propio panel del detalle. Una tarea Resuelta deja de mostrar semáforo.",
    },
    {
      titulo: "Actualizá el % de avance",
      etiquetaIndice: "% de avance",
      cuerpo:
        "En el campo % de avance cargá un número entero entre 0 y 100 y pulsá Guardar. El avance es independiente del estado: poner una tarea en Resuelta no lo lleva a 100 ni al revés.",
    },
    {
      titulo: "Cancelá una tarea (autoridad creadora)",
      etiquetaIndice: "Cancelar",
      cuerpo:
        "Solo quien creó la tarea ve el botón Cancelar, arriba. Pide confirmación en «Cancelar tarea»; queda registrado en el Historial. Una tarea Cancelada o Resuelta solo la puede volver a abrir (pasarla a Pendiente, En curso o Pausa) esa misma autoridad.",
      nota: {
        tipo: "importante",
        cuerpo:
          "Quien es responsable de la tarea pero no la creó no puede cancelarla ni reabrirla una vez terminada. El servidor también lo rechaza.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "El estado nuevo se ve de inmediato en el detalle y en el listado, y el cambio queda en el Historial con su autor y fecha.",
  },
  preguntas: [
    {
      pregunta: "No veo el panel Acciones.",
      respuesta:
        "Solo lo ven el responsable de la tarea y la autoridad que la creó (y el Administrador de Sistemas). Si ves la tarea pero no es tuya, tenés acceso de lectura, comentarios y relaciones.",
    },
    {
      pregunta: "Resolví una tarea por error.",
      respuesta:
        "Pedile a la autoridad que la creó que la reabra: es la única que puede sacarla de Resuelta.",
    },
    {
      pregunta: "¿Qué pasa con las tareas hijas al resolver la tarea padre?",
      respuesta:
        "Nada: cada tarea hija tiene su propio estado y avance, y no se calculan a partir de las demás.",
    },
  ],
  relacionados: ["tareas/abrir-una-tarea", "tareas/crear-una-tarea"],
};
