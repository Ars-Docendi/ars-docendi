import type { ArticuloAyuda } from "../../tipos";

export const descargarElLoteDeFinalizados: ArticuloAyuda = {
  slug: "descargar-el-lote-de-finalizados",
  moduloSlug: "designaciones",
  grupo: "Revisar pedidos",
  titulo: "Cómo descargar el lote de finalizados",
  resumen: "Exportar el libro de pedidos finalizados del período activo.",
  introduccion:
    "El lote es un libro de Excel con las altas, bajas y cambios aprobados del período activo, más las designaciones que continúan vigentes. Lo descargan Secretaría Académica, Decanato y Administración desde la pestaña Finalizados.",
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
      titulo: "Andá a la pestaña Finalizados",
      etiquetaIndice: "Finalizados",
      cuerpo:
        "En Revisión, elegí la pestaña Finalizados. Si tu rol puede descargar el lote, a la derecha de las pestañas aparecen el texto Período con el nombre del período activo y el botón Exportar.",
      captura: {
        src: "/ayuda/designaciones/descargar-el-lote-de-finalizados-paso-1.webp",
        alt: "Pestaña Finalizados con «Período: …» y el botón Exportar a la derecha de las pestañas.",
      },
    },
    {
      titulo: "Pulsá Exportar",
      etiquetaIndice: "Exportar",
      cuerpo:
        "El botón muestra Exportando… mientras se arma el archivo, y el navegador descarga lote-designaciones.xlsx. Mientras dura la descarga no se pueden lanzar otras desde el botón.",
    },
    {
      titulo: "Abrí el libro",
      etiquetaIndice: "Contenido del libro",
      cuerpo:
        "El libro tiene tres hojas. PROPUESTA COMPLETA: una fila por asignación vigente, baja aprobada o alta aprobada del período. ALTAS: los pedidos de Alta aprobados. BAJAS: los pedidos de Baja aprobados, con el cargo anterior y el motivo. Los datos que el sistema no tiene (por ejemplo CUIL o teléfono) quedan en blanco.",
    },
    {
      titulo: "Si algo falla",
      etiquetaIndice: "Errores",
      cuerpo:
        "Sin período activo, Exportar queda deshabilitado con la explicación «Configurá un período activo para descargar el lote». Si el período cambió mientras tenías la pantalla abierta, ves «El período cambió. Actualizamos los períodos; reintentá la descarga.». Ante cualquier otro error, el sistema muestra «No se pudo descargar el lote. Reintentá.»",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Descargar el lote no modifica pedidos, designaciones ni estados: podés repetirlo cuantas veces quieras. El libro refleja el estado del momento de la descarga e incluye todo el período activo aunque la tabla tenga filtros aplicados.",
  },
  preguntas: [
    {
      pregunta: "No veo el botón Exportar. ¿Por qué?",
      respuesta:
        "Solo lo ven Secretaría Académica, Decanato y Administración, y solo en la pestaña Finalizados. El resto de los roles no puede descargar el lote.",
    },
    {
      pregunta: "¿El lote incluye el período que tengo elegido en el filtro?",
      respuesta:
        "No: siempre usa el período activo, el que figura junto al botón. Los filtros de la tabla no recortan el contenido del libro.",
    },
  ],
  relacionados: ["designaciones/revisar-el-tablero", "designaciones/gestionar-periodos"],
};
