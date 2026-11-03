/**
 * Los chips «Módulo» de la pestaña Auditoría (design D13, sistema-seccion-
 * unificada): una lista estática, no descubierta en runtime — «derivada de
 * los módulos con datos auditados» es una decisión de producto, no un
 * endpoint. Aulas y Tareas no tienen tablas auditadas y no llevan chip; el
 * día que las tengan, se agrega acá.
 */
export const MODULOS_AUDITORIA = [
  { valor: "identity", etiqueta: "Identidad" },
  { valor: "designaciones", etiqueta: "Designaciones" },
  { valor: "portal", etiqueta: "Portal" },
  { valor: "asistente", etiqueta: "Asistente" },
] as const;
