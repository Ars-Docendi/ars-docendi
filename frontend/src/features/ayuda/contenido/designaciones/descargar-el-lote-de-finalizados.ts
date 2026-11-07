import type { ArticuloAyuda } from "../../tipos";

export const descargarElLoteDeFinalizados: ArticuloAyuda = {
  slug: "descargar-el-lote-de-finalizados",
  moduloSlug: "designaciones",
  grupo: "Revisar pedidos",
  titulo: "Cómo descargar el lote de finalizados",
  resumen: "Exportar el libro de pedidos finalizados del período activo.",
  introduccion:
    "El lote es un libro de Excel con las altas, bajas y cambios aceptados por Decanato en el período activo, más las designaciones que continúan vigentes. Va a la rectificación y, cuando vuelve, se concilia en la pantalla Conciliación. Lo descarga quien tiene el permiso de exportar el lote (de inicio, Secretaría Académica) desde el encabezado de Revisión.",
  permisos: ["designaciones.revisar"],
  palabrasClave: [
    "lote",
    "exportar",
    "descargar",
    "excel",
    "xlsx",
    "finalizados",
    "planilla",
    "propuesta completa",
    "altas",
    "bajas",
    "período activo",
  ],
  pasos: [
    {
      titulo: "Abrí Revisión",
      etiquetaIndice: "Revisión",
      cuerpo:
        "Si tenés el permiso, a la derecha del título Revisión aparece el botón Exportar lote, en cualquier pestaña. Debajo del título, el texto Período indica el período activo que se va a descargar.",
    },
    {
      titulo: "Pulsá Exportar lote",
      etiquetaIndice: "Exportar lote",
      cuerpo:
        "El botón muestra Exportando… mientras se arma el archivo, y el navegador descarga lote-designaciones.xlsx. Mientras dura la descarga no se pueden lanzar otras desde el botón.",
    },
    {
      titulo: "Abrí el libro",
      etiquetaIndice: "Contenido del libro",
      cuerpo:
        "El libro tiene tres hojas. PROPUESTA COMPLETA: una fila por asignación vigente, baja o alta del lote; un cambio todavía sin sincronizar muestra el cargo actual como anterior y lo pedido como propuesta. ALTAS: los pedidos de Alta del lote. BAJAS: los pedidos de Baja del lote, con el cargo anterior y el motivo. Un pedido rechazado en la conciliación ya no aparece. Los datos que el sistema no tiene (por ejemplo CUIL o teléfono) quedan en blanco. Arriba a la derecha de la primera hoja, la celda «INTEGRIDAD · sello …» identifica el estado exacto del período que se exportó.",
    },
    {
      titulo: "Si algo falla",
      etiquetaIndice: "Errores",
      cuerpo:
        "Sin período activo, Exportar lote queda deshabilitado y debajo del título se lee «Sin período activo: configuralo para descargar el lote.». Si el período cambió mientras tenías la pantalla abierta, ves «El período cambió. Actualizamos los períodos; reintentá la descarga.». Si el sistema detecta que algún pedido del período se modificó por fuera del circuito, el lote no se descarga y ves «No se pudo verificar la integridad del período y el lote no se descargó. Avisá a la administración del sistema.». Estos avisos aparecen bajo el encabezado como «No se descargó el lote». Ante cualquier otro error, el sistema muestra «No se pudo descargar el lote. Reintentá.»",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Descargar el lote no modifica pedidos, designaciones ni estados: podés repetirlo cuantas veces quieras. Las designaciones se actualizan recién al sincronizar en Conciliación. Sólo deja registrado el código de integridad que figura en el libro. El libro refleja el estado del momento de la descarga e incluye todo el período activo aunque la tabla tenga filtros aplicados.",
  },
  preguntas: [
    {
      pregunta: "¿Para qué sirve la celda INTEGRIDAD del libro?",
      respuesta:
        "Es un código que resume todo el circuito de los pedidos del período hasta ese momento. Si después alguien alterara un pedido o su historial por fuera del sistema, el código dejaría de coincidir con el del libro que ya circuló.",
    },
    {
      pregunta: "No veo el botón Exportar lote. ¿Por qué?",
      respuesta:
        "Solo lo ve quien tiene el permiso de exportar el lote. De inicio lo tiene Secretaría Académica; la administración del sistema puede concedérselo a otro rol.",
    },
    {
      pregunta: "¿El lote incluye el período que tengo elegido en el filtro?",
      respuesta:
        "No: siempre usa el período activo, el que figura debajo del título. Los filtros de la tabla no recortan el contenido del libro.",
    },
  ],
  relacionados: [
    "designaciones/revisar-el-tablero",
    "designaciones/conciliar-el-lote",
    "designaciones/gestionar-periodos",
  ],
};
