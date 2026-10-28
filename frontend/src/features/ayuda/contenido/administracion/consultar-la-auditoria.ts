import type { ArticuloAyuda } from "../../tipos";

export const consultarLaAuditoria: ArticuloAyuda = {
  slug: "consultar-la-auditoria",
  moduloSlug: "administracion",
  grupo: "Sistema",
  titulo: "Cómo consultar los registros de auditoría",
  resumen: "Filtrar y leer los registros de auditoría.",
  introduccion:
    "Registros de auditoría es la consulta de solo lectura de los cambios que quedaron guardados en el sistema: quién cambió qué y cuándo. No se puede modificar ni borrar nada desde esta pantalla.",
  permisos: ["auditoria.ver"],
  palabrasClave: [
    "auditoría",
    "registros",
    "quién cambió",
    "historial de cambios",
    "filtros",
    "actor",
    "acción",
    "tabla",
    "clave de fila",
    "detalle",
    "enmascarado",
    "paginación",
  ],
  pasos: [
    {
      titulo: "Abrí Registros de auditoría",
      etiquetaIndice: "Abrir la auditoría",
      cuerpo:
        "En el menú, grupo Sistema, elegí Registros de auditoría. El título indica cuántos registros hay. La tabla muestra, del más reciente al más antiguo, Fecha, Usuario, Acción, Módulo y Cambio (un resumen).",
      captura: {
        src: "/ayuda/administracion/consultar-la-auditoria-paso-1.webp",
        alt: "Tabla de Registros de auditoría con el formulario de filtros.",
      },
    },
    {
      titulo: "Filtrá los registros",
      etiquetaIndice: "Filtrar",
      cuerpo:
        "Arriba tenés el formulario de filtros: Desde y Hasta (fecha y hora), Acción (Todas, Alta, Actualización o Eliminación física), Módulo o schema (por ejemplo «Identidad»), Tabla, Actor (por nombre) y Clave de fila. Completá los que necesites y pulsá Buscar; Limpiar vuelve a la lista completa. Si nada coincide, la pantalla dice «No hay registros para los filtros seleccionados.»",
      nota: {
        tipo: "consejo",
        titulo: "Cómo buscar a una persona",
        cuerpo:
          "Escribí parte de su nombre en Actor para ver lo que cambió esa persona. Para ver la historia de un registro puntual, pegá su Clave de fila completa (el JSON tal como figura en el detalle): una clave parcial consulta una agrupación histórica, no una fila individual.",
      },
    },
    {
      titulo: "Leé cada registro",
      etiquetaIndice: "Leer un registro",
      cuerpo:
        "La columna Usuario muestra el nombre de quien hizo el cambio, o «Actor no identificado» si no se puede determinar. Acción dice si fue un alta, una actualización o una eliminación física. Cambio resume qué se modificó, sobre qué objeto y en qué tabla.",
    },
    {
      titulo: "Abrí el detalle",
      etiquetaIndice: "Ver detalle",
      cuerpo:
        "Pulsá Ver detalle en un registro. Aparecen la Tabla (schema.tabla), la Clave de fila y la Solicitud, y una tabla con Campo, Anterior y Nuevo para cada campo que cambió. Ocultar detalle lo cierra.",
      captura: {
        src: "/ayuda/administracion/consultar-la-auditoria-paso-4.webp",
        alt: "Detalle de un registro con los campos anterior y nuevo.",
      },
      nota: {
        tipo: "importante",
        titulo: "Valores enmascarados",
        cuerpo:
          "Por política, los datos personales y los campos sensibles figuran como «Enmascarado por política», y los campos sin clasificar no muestran su valor. La auditoría dice qué campo cambió, no siempre su contenido.",
      },
    },
    {
      titulo: "Recorré las páginas",
      etiquetaIndice: "Paginar",
      cuerpo:
        "Se muestran 50 registros por página. Usá Anterior y Siguiente (la barra indica «Página 1 de N») y Actualizar para volver a consultar. Al cambiar los filtros se vuelve a la primera página.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Esta pantalla solo consulta. Si encontrás un cambio que no esperabas, usá el actor, la fecha y la clave de fila para reconstruir qué pasó y llevalo a quien administra el sistema.",
  },
  preguntas: [
    {
      pregunta: "¿Por qué hay registros con «Actor no identificado»?",
      respuesta:
        "Porque el cambio no quedó asociado a una cuenta que se pueda resolver. El sistema no se lo atribuye a nadie ni a un proceso automático sin evidencia.",
    },
    {
      pregunta: "Un cambio reciente no aparece.",
      respuesta:
        "La lista no se actualiza sola: pulsá Actualizar. Si usás filtros de fecha, revisá que Hasta incluya el momento del cambio.",
    },
  ],
  relacionados: [
    "administracion/leer-el-dashboard-del-sistema",
    "administracion/administrar-usuarios",
  ],
};
