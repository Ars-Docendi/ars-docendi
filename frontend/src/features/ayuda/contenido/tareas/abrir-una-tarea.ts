import type { ArticuloAyuda } from "../../tipos";

export const abrirUnaTarea: ArticuloAyuda = {
  slug: "abrir-una-tarea",
  moduloSlug: "tareas",
  grupo: "Seguir tareas",
  titulo: "Cómo abrir y leer una tarea",
  resumen: "Detalle, comentarios, tareas hijas y relacionadas.",
  introduccion:
    "El detalle de una tarea reúne todo lo que hace falta para trabajarla: qué hay que hacer, quién es el responsable, cómo avanza y qué se habló. Desde acá también se comenta, se relaciona con otras tareas y, según tu rol, se edita o se cambia de estado.",
  permisos: ["tareas.ver"],
  palabrasClave: [
    "detalle",
    "tarea",
    "comentarios",
    "comentar",
    "historial",
    "tarea hija",
    "tareas relacionadas",
    "relacionar",
    "solución",
    "avance",
    "responsable",
    "autor",
    "tarea padre",
  ],
  pasos: [
    {
      titulo: "Entrá al detalle",
      etiquetaIndice: "Entrar",
      cuerpo:
        "En Tareas, hacé clic en la fila (o en Ver). Arriba ves el título, la prioridad, el responsable y el estado. El botón Volver te lleva a la pantalla anterior. Si la tarea no existe o no es tuya, el sistema muestra «No se encontró la tarea» con un enlace para volver a Tareas.",
      captura: {
        src: "/ayuda/tareas/abrir-una-tarea-paso-1.webp",
        alt: "Detalle de una tarea con título, estado y botones de la cabecera.",
      },
    },
    {
      titulo: "Leé la descripción y la solución",
      etiquetaIndice: "Descripción",
      cuerpo:
        "El panel Descripción explica qué hay que hacer («Sin descripción.» si quien la creó no la completó). Cuando la tarea se resuelve, aparece además el panel Solución con lo que escribió el responsable.",
    },
    {
      titulo: "Mirá los Datos de la tarea",
      etiquetaIndice: "Datos",
      cuerpo:
        "A la derecha, el panel Datos muestra Tipo, Fecha de inicio, Fecha de fin (con el semáforo si la tarea sigue abierta), % de avance, Responsable, Autor y Proyecto. Si la tarea pertenece a un proyecto, su nombre es un enlace al detalle del proyecto; si es una hija, aparece también Tarea padre.",
      captura: {
        src: "/ayuda/tareas/abrir-una-tarea-paso-3.webp",
        alt: "Panel Datos con proyecto y semáforo.",
      },
    },
    {
      titulo: "Seguí las tareas hijas",
      etiquetaIndice: "Tareas hijas",
      cuerpo:
        "Una tarea compleja se puede descomponer en tareas hijas. Cada hija tiene su propio responsable, fechas, estado y avance: el avance del padre no se calcula a partir de las hijas. Si tu rol crea tareas, el botón Nueva tarea hija abre el formulario con el proyecto ya heredado del padre.",
    },
    {
      titulo: "Relacioná tareas",
      etiquetaIndice: "Relacionadas",
      cuerpo:
        "En Tareas relacionadas escribí en «Buscar una tarea para relacionar…» y elegí una: el vínculo se crea al instante y se ve desde las dos tareas. Con Quitar se deshace. La relación es un acceso rápido; no cambia estados ni avances.",
    },
    {
      titulo: "Comentá",
      etiquetaIndice: "Comentarios",
      cuerpo:
        "En Comentarios internos escribí en «Escribí un comentario…» y pulsá Comentar. Lo ve cualquiera que tenga acceso a la tarea, con autor, rol y fecha. Los comentarios no se editan ni se borran.",
    },
    {
      titulo: "Revisá el Historial",
      etiquetaIndice: "Historial",
      cuerpo:
        "El Historial registra quién creó la tarea, cada cambio de estado, cada actualización de avance y cada edición de campos, con su fecha.",
    },
  ],
  despues: {
    titulo: "¿Qué sigue?",
    cuerpo:
      "Si sos responsable, usá el panel Acciones de la derecha para cambiar el estado o actualizar el avance. Si sos la autoridad que creó la tarea, además vas a ver Editar y Cancelar arriba.",
    viñetas: [
      {
        termino: "Editar",
        descripcion:
          "Cambia título, descripción, fechas, prioridad, tipo, responsable y proyecto. Solo lo ve la autoridad que creó la tarea.",
      },
      {
        termino: "Cancelar",
        descripcion:
          "Anula una tarea no terminada, con confirmación. Solo la autoridad que la creó puede hacerlo y reabrirla.",
      },
    ],
  },
  preguntas: [
    {
      pregunta: "Edité una tarea y el sistema dice «El recurso cambió».",
      respuesta:
        "Alguien la modificó mientras la editabas, y el sistema no pisa los cambios ajenos. Cerrá el formulario, volvé a abrir Editar para ver la versión actual y rehacé tu cambio: reintentar Guardar cambios con el formulario viejo seguirá fallando.",
    },
    {
      pregunta: "¿Por qué la hora de un comentario no coincide con mi reloj?",
      respuesta:
        "Los comentarios y las fechas del historial se muestran en hora universal (UTC), que en Argentina está tres horas adelantada. Un comentario de las 23:00 puede figurar con la fecha del día siguiente.",
    },
  ],
  relacionados: [
    "tareas/cambiar-el-estado-de-una-tarea",
    "tareas/entender-el-listado-de-tareas",
    "tareas/crear-una-tarea",
  ],
};
