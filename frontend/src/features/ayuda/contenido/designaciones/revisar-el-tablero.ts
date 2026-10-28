import type { ArticuloAyuda } from "../../tipos";

export const revisarElTablero: ArticuloAyuda = {
  slug: "revisar-el-tablero",
  moduloSlug: "designaciones",
  grupo: "Revisar pedidos",
  titulo: "Cómo usar el tablero de Revisión",
  resumen: "Pestañas por área, filtros por encabezado y orden de los pedidos.",
  introduccion:
    "Revisión reúne los pedidos de tu ámbito en una sola tabla con pestañas por área del circuito. Un Coordinador ve los de su carrera; Secretaría, Decanato y Administración ven los de todo el departamento.",
  permisos: ["designaciones.revisar"],
  palabrasClave: [
    "revisión",
    "tablero",
    "revisar pedidos",
    "pestañas",
    "coordinación",
    "secretaría",
    "decanato",
    "finalizados",
    "filtrar",
    "buscar docente",
    "prioritario",
    "sin movimiento",
    "pendientes",
  ],
  pasos: [
    {
      titulo: "Abrí Revisión",
      etiquetaIndice: "Abrir Revisión",
      cuerpo:
        "En el menú, grupo Designaciones, elegí Revisión. El título aclara el alcance: «Pedidos en tu ámbito», seguido de tu carrera o del departamento. La pantalla abre en la pestaña de tu propia área (por ejemplo, En Coordinación para el Coordinador) o en Todos si tu rol no tiene un área propia.",
      captura: {
        src: "/ayuda/designaciones/revisar-el-tablero-paso-1.webp",
        alt: "Revisión de un Coordinador con las pestañas y el contador de cada una.",
      },
    },
    {
      titulo: "Elegí la pestaña",
      etiquetaIndice: "Pestañas",
      cuerpo:
        "Las pestañas son Todos, En Cátedra, En Coordinación, En Secretaría, En Decanato y Finalizados, cada una con su cantidad de pedidos. Cada pedido está donde está hoy: uno devuelto figura en el área que tiene que corregirlo. Todos no incluye borradores, y Finalizados junta los pedidos aprobados, rechazados y cancelados.",
    },
    {
      titulo: "Filtrá los pedidos",
      etiquetaIndice: "Filtros",
      cuerpo:
        "Arriba de las pestañas, el selector Filtrar por período arranca en el período abierto. Con + Añadir filtro sumás Prioridad (solo prioritarios o sin prioridad), Sin movimiento (más de 7, 15 o 30 días sin cambios) y, si ves más de una carrera, Carrera. Además cada encabezado de la tabla filtra por columna: texto para Docente y Legajo, casillas para Tipo y Estado.",
      captura: {
        src: "/ayuda/designaciones/revisar-el-tablero-paso-3.webp",
        alt: "Filtros superiores con el filtro Sin movimiento agregado y un encabezado con su filtro desplegado.",
      },
    },
    {
      titulo: "Ordená la tabla",
      etiquetaIndice: "Ordenar",
      cuerpo:
        "Las columnas son Docente, Legajo, Tipo, Inicio, Últ. actualización y Estado (y Área en la pestaña Todos). Sin un orden elegido, la tabla pone primero los pedidos prioritarios, después los devueltos y después los que hace más tiempo esperan. Hacé clic en un encabezado para ordenar ascendente, otra vez para descendente y una tercera para volver al orden por defecto.",
    },
    {
      titulo: "Abrí un pedido",
      etiquetaIndice: "Abrir un pedido",
      cuerpo:
        "Hacé clic en una fila (o enfocala y pulsá Enter o Espacio) para abrir el detalle del pedido, donde revisás y resolvés. Si no hay pedidos que cumplan los filtros, la tabla dice «Sin pedidos que cumplan los filtros.»",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Desde el detalle del pedido aprobás, rechazás, devolvés o marcás como prioritario. Cuando volvés, el tablero refleja el cambio y los contadores de las pestañas se actualizan.",
  },
  preguntas: [
    {
      pregunta: "¿Por qué no veo pedidos de otra carrera?",
      respuesta:
        "El Coordinador solo ve y actúa sobre los pedidos de su carrera. Secretaría, Decanato y Administración tienen alcance sobre todo el departamento.",
    },
    {
      pregunta: "¿Por qué no veo los pedidos de períodos anteriores?",
      respuesta:
        "El selector Filtrar por período arranca en el período abierto. Cambialo a otro período o a Todos los períodos para ver los demás.",
    },
  ],
  relacionados: [
    "designaciones/resolver-un-pedido",
    "designaciones/descargar-el-lote-de-finalizados",
    "designaciones/seguir-el-estado-de-un-pedido",
  ],
};
