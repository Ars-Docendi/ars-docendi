import type { ArticuloAyuda } from "../../tipos";

export const consultarLasComisiones: ArticuloAyuda = {
  slug: "consultar-las-comisiones",
  moduloSlug: "designaciones",
  grupo: "Comisiones",
  titulo: "Cómo consultar las comisiones importadas de Guaraní",
  resumen:
    "Ver las comisiones de un período lectivo, filtrarlas, exportarlas a Excel y actualizarlas desde Guaraní.",
  introduccion:
    "La pantalla Comisiones muestra las comisiones importadas desde Guaraní para cada período lectivo: materia, carreras, aulas, bandas horarias, docentes, modalidad y ubicación. Es de solo lectura: lo que ves es la última importación, y los datos se corrigen en Guaraní.",
  permisos: ["comisiones.ver"],
  palabrasClave: [
    "comisión",
    "comisiones",
    "guaraní",
    "importar",
    "actualizar",
    "período lectivo",
    "banda horaria",
    "horarios",
    "aula",
    "modalidad",
    "excel",
    "exportar",
    "descargar",
  ],
  pasos: [
    {
      titulo: "Abrí Comisiones",
      etiquetaIndice: "Abrir Comisiones",
      cuerpo:
        "En el menú, grupo Designaciones, elegí Comisiones. Si todavía no se importó ningún período, la pantalla lo indica. Debajo del encabezado ves cuándo y quién hizo la última actualización.",
    },
    {
      titulo: "Elegí el año y el período lectivo",
      etiquetaIndice: "Elegir año y período",
      cuerpo:
        "Los selectores ofrecen los años y períodos con comisiones importadas, y arrancan en el más reciente. Al cambiar de año se elige el período más reciente de ese año. La tabla muestra las comisiones del período elegido, sin paginación.",
    },
    {
      titulo: "Filtrá la tabla",
      etiquetaIndice: "Filtrar",
      cuerpo:
        "Los encabezados Materia, Carreras, Docentes y Modalidad tienen filtros de texto, que no distinguen mayúsculas ni tildes. Materia y Carreras buscan por nombre o por código. El año, el período y los filtros quedan en la dirección de la página, así que se conservan al recargar.",
    },
    {
      titulo: "Exportá lo filtrado a Excel",
      etiquetaIndice: "Exportar",
      cuerpo:
        "Pulsá Exportar a Excel para descargar las comisiones que estás viendo, con el período y los filtros aplicados. Cada comisión ocupa una fila, con las mismas columnas de la tabla. El botón se deshabilita si los filtros no dejan ninguna comisión. Sale de los datos ya importados, así que no hace falta conexión con Guaraní.",
    },
    {
      titulo: "Actualizá un período desde Guaraní",
      etiquetaIndice: "Actualizar",
      cuerpo:
        "Si tu rol lo permite, pulsá Actualizar desde Guaraní, elegí Año y Período lectivo y confirmá con Importar. Se reemplazan solo las comisiones de ese período; los demás no cambian. Al terminar, un aviso resume cuántas comisiones se importaron y cuántos docentes o carreras no figuran en el sistema.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "La tabla pasa a mostrar el período importado. Si una actualización falla, la pantalla lo avisa y sigue mostrando las comisiones de la última importación exitosa.",
  },
  preguntas: [
    {
      pregunta: "No veo el botón Actualizar desde Guaraní. ¿Es un error?",
      respuesta:
        "No: importar requiere un permiso propio, que tienen Secretaría Académica y el Administrador de Sistemas. Sin él podés consultar las comisiones ya importadas.",
    },
    {
      pregunta: "¿Por qué no puedo importar en este ambiente?",
      respuesta:
        "Guaraní solo responde dentro de la red de la UNLaM, así que la importación únicamente está disponible donde esa conexión está configurada. Las comisiones ya importadas se consultan igual.",
    },
  ],
  relacionados: ["designaciones/gestionar-periodos"],
};
