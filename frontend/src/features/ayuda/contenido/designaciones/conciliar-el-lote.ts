import type { ArticuloAyuda } from "../../tipos";

export const conciliarElLote: ArticuloAyuda = {
  slug: "conciliar-el-lote",
  moduloSlug: "designaciones",
  grupo: "Revisar pedidos",
  titulo: "Cómo conciliar el lote",
  resumen: "Aceptar, rechazar o corregir los pedidos en lote y sincronizarlos.",
  introduccion:
    "Cuando vuelve la rectificación del lote, la pantalla Conciliación te deja comparar, docente por docente, lo que tiene hoy con lo que queda. Aceptás a los que coinciden, rechazás lo que no se aprobó, corregís lo que cambió y sincronizás. Recién al sincronizar se actualizan las designaciones. La ven por defecto Secretaría Académica y Administrativos.",
  permisos: ["designaciones.conciliar"],
  palabrasClave: [
    "conciliar",
    "conciliación",
    "rectificación",
    "sincronizar",
    "lote",
    "en lote",
    "aprobado",
    "rechazado en conciliación",
    "aceptar docente",
    "designación",
  ],
  pasos: [
    {
      titulo: "Abrí Conciliación",
      etiquetaIndice: "Abrir",
      cuerpo:
        "En la navegación, Designaciones → Conciliación. La pestaña Por conciliar muestra una fila por docente con pedidos en lote del período activo: su CUIL, las carreras, los cambios (Alta, Cambio, Baja) y con cuántas materias queda. Si no hay nada, la pantalla aclara que acá se ven únicamente los pedidos en lote del período activo.",
    },
    {
      titulo: "Revisá cómo queda cada docente",
      etiquetaIndice: "Revisar",
      cuerpo:
        "Pulsá la fila para ver el detalle por materia: Hoy es la designación vigente y Queda es lo que pide el pedido, con las horas de investigación y externas, la justificación de un cambio y el tipo de baja. Las materias sin pedido figuran como Sin cambios. Al pie ves con cuántas materias y horas semanales queda. Un docente marcado Docente nuevo no tiene otra designación: el pedido es su primer cargo.",
    },
    {
      titulo: "Rechazá lo que no se aprobó",
      etiquetaIndice: "Rechazar",
      cuerpo:
        "En la fila del pedido, Rechazar pide un motivo obligatorio. El cambio no se aplica, sale del Excel del lote y pasa a la pestaña Rechazados. El docente queda sin aceptar para que lo revises de nuevo.",
    },
    {
      titulo: "Corregí lo que cambió",
      etiquetaIndice: "Editar",
      cuerpo:
        "Editar abre el formulario completo del pedido (hace falta el permiso de editar designaciones). Al guardar volvés a Conciliación y el docente queda sin aceptar. Un rechazado se corrige con Editar y volver a conciliar, desde la pestaña Rechazados: al guardar vuelve a Por conciliar.",
    },
    {
      titulo: "Aceptá y sincronizá",
      etiquetaIndice: "Sincronizar",
      cuerpo:
        "Tildá a los docentes que coinciden con la rectificación, o la casilla del encabezado para todos los visibles. Aceptar un docente acepta todos sus cambios. Pulsá Sincronizar, junto a las pestañas: la vista previa muestra cuántos docentes nuevos, altas, cambios y bajas se aplican, el detalle de cada uno y qué se actualiza en la base de docentes. Confirmá con Sincronizar.",
      nota: {
        tipo: "importante",
        titulo: "No se puede deshacer",
        cuerpo:
          "Los pedidos sincronizados pasan a Aprobado y ya no se pueden editar. Si algún pedido cambió mientras conciliabas, no se sincroniza nada y te avisa cuál: revisalo y volvé a aceptarlo.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Cada pedido sincronizado queda en la pestaña Sincronizados con la fecha y quién lo sincronizó, y el Jefe de Cátedra lo ve Aprobado.",
    viñetas: [
      {
        termino: "Alta o Cambio",
        descripcion:
          "Se escribe la designación con cargo, dedicación y horas. Si la persona tiene cuenta, recibe el rol Docente en esa materia y carrera.",
      },
      {
        termino: "Baja",
        descripcion: "Se cierra la designación vigente y se retira el rol Docente en esa materia.",
      },
      {
        termino: "Docente nuevo",
        descripcion:
          "Alguien sin otra designación vigente: el Alta es su primer cargo. La persona ya existe desde que se creó el pedido, con sus datos y sin mail. Al cargarle el mail en Personas, el rol Docente aparece precargado según sus designaciones.",
      },
    ],
  },
  preguntas: [
    {
      pregunta: "¿Por qué no puedo tildar a un docente?",
      respuesta:
        "Porque alguno de sus pedidos en lote está fuera de tu ámbito. Si el docente tiene un pedido rechazado, eso no impide aceptar el resto.",
    },
    {
      pregunta: "¿Un pedido rechazado en la conciliación vuelve al circuito de revisión?",
      respuesta:
        "No: ya pasó Coordinación, Secretaría y Decanato. Se corrige desde Conciliación y vuelve a Por conciliar.",
    },
    {
      pregunta: "Hay un pedido en lote sin botón Rechazar. ¿Por qué?",
      respuesta:
        "Es un pedido aceptado antes de que existiera la conciliación: su designación ya se escribió en ese momento. Sólo se sincroniza para cerrarlo.",
    },
  ],
  relacionados: [
    "designaciones/descargar-el-lote-de-finalizados",
    "designaciones/editar-un-pedido",
    "designaciones/seguir-el-estado-de-un-pedido",
  ],
};
