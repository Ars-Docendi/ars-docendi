import type { ArticuloAyuda } from "../../tipos";

export const leerElDashboardDelSistema: ArticuloAyuda = {
  slug: "leer-el-dashboard-del-sistema",
  moduloSlug: "administracion",
  grupo: "Sistema",
  titulo: "Cómo leer el dashboard del sistema",
  resumen: "Estado de módulos, base de datos e inferencia.",
  introduccion:
    "El Dashboard del sistema muestra, en una sola tabla, si cada parte del sistema responde en este momento. Sirve para confirmar si un problema es general o puntual antes de avisar a soporte.",
  permisos: ["sistema.estado.ver"],
  palabrasClave: [
    "dashboard",
    "estado del sistema",
    "salud",
    "disponible",
    "no disponible",
    "base de datos",
    "postgresql",
    "inferencia",
    "asistente",
    "módulos",
    "caído",
    "no funciona",
    "actualizar",
  ],
  pasos: [
    {
      titulo: "Abrí el Dashboard",
      etiquetaIndice: "Abrir el Dashboard",
      cuerpo:
        "En el menú, grupo Sistema, elegí Dashboard del sistema. Al abrirlo consulta el estado actual y muestra «Consultando el estado actual…» hasta tener la respuesta.",
      captura: {
        src: "/ayuda/administracion/leer-el-dashboard-del-sistema-paso-1.webp",
        alt: "Dashboard del sistema con la tabla de componentes.",
      },
    },
    {
      titulo: "Leé la tabla Estado por componente",
      etiquetaIndice: "Leer la tabla",
      cuerpo:
        "Cada fila es un componente: los módulos Aulas, Tareas, Designaciones y Portal, más PostgreSQL e Inferencia (IA). Las columnas son Componente, Estado, Duración (en milisegundos) y Última comprobación (fecha y hora). Cada resultado es independiente: que uno falle no oculta a los demás.",
    },
    {
      titulo: "Interpretá el estado",
      etiquetaIndice: "Interpretar el estado",
      cuerpo:
        "Disponible: respondió bien. No disponible: falló o tardó más de cinco segundos. Deshabilitado: el ambiente no tiene ese servicio configurado (es normal en la inferencia, y se muestra distinto de «No disponible»). Desconocido: respondió algo que el sistema no pudo interpretar.",
      captura: {
        src: "/ayuda/administracion/leer-el-dashboard-del-sistema-paso-3.webp",
        alt: "Filas con distintos estados.",
      },
      nota: {
        tipo: "info",
        cuerpo:
          "Para los módulos, la comprobación es la respuesta del módulo a una consulta simple; PostgreSQL y la inferencia las verifica el backend. Un módulo disponible no garantiza que todas sus funciones anden, solo que está respondiendo.",
      },
    },
    {
      titulo: "Actualizá",
      etiquetaIndice: "Actualizar",
      cuerpo:
        "El dashboard no se refresca solo ni al volver a la pestaña: pulsá Actualizar, arriba a la derecha, para volver a medir. Si la consulta completa falla, aparece «No se pudo cargar el estado del sistema.» con un botón Reintentar.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "El dashboard es de solo lectura: no reinicia ni modifica nada. Si un componente figura como No disponible de forma sostenida, informalo al equipo que mantiene el sistema con el nombre del componente y la hora de la última comprobación.",
  },
  preguntas: [
    {
      pregunta: "La inferencia figura como Deshabilitado. ¿Es un problema?",
      respuesta:
        "No necesariamente: significa que este ambiente no tiene configurado el servicio de inferencia. No Disponible, en cambio, indica que está configurado pero no responde.",
    },
    {
      pregunta: "¿Muestra datos de personas o de las bases?",
      respuesta:
        "No. Solo nombres de componentes, estados, duraciones y horas. Si la base falla, el mensaje no expone datos de conexión.",
    },
  ],
  relacionados: ["administracion/consultar-la-auditoria"],
};
