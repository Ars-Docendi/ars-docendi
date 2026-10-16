/**
 * Nombre legible de cada código de rol de sistema (tarea «rol y cupo
 * efectivo por usuario» de sistema-seccion-unificada, panel de uso del
 * asistente).
 *
 * El backend expone `codigosDeRol` — nunca un nombre — porque
 * `IConsultasIdentity` (la única puerta por la que el módulo lee identity,
 * regla 4 de AGENTS.md) no expone ningún catálogo de roles con nombre; sólo
 * `ObtenerCodigosDeRolesDeSistemaAsync`, que devuelve códigos. La traducción
 * vive acá, del lado del cliente, con el mismo catálogo fijo de los siete
 * roles de sistema que sembra `database/identity/002_identity_roles.sql` —
 * el mismo catálogo cerrado del que design.md D13 ya toma los chips
 * estáticos de módulo en la Auditoría.
 */
const ETIQUETAS_DE_ROL: Record<string, string> = {
  docente: "Docente",
  jefe_catedra: "Jefe de Cátedra",
  coordinador_carrera: "Coordinador de Carrera",
  secretaria: "Secretaría Académica",
  decanato: "Decanato",
  administrativo: "Administrativo",
  sys_admin: "Administrador de Sistemas",
};

/** El nombre legible de un código de rol, o el código mismo si no está en el catálogo. */
export function etiquetaDeRol(codigo: string): string {
  return ETIQUETAS_DE_ROL[codigo] ?? codigo;
}

/**
 * El subtítulo de rol para una fila de usuario: los nombres legibles de
 * TODOS sus roles vigentes, unidos — un actor puede tener más de uno (por
 * ejemplo, Jefe de Cátedra y Docente a la vez) — o `undefined` si no tiene
 * ninguno (mismo caso que deja `cupoEfectivo`/`origenDeCupo` en `null`, ver
 * `ReglaDeCupoEfectivo` en el backend).
 */
export function subtituloDeRoles(codigosDeRol: string[]): string | undefined {
  if (codigosDeRol.length === 0) return undefined;
  return codigosDeRol.map(etiquetaDeRol).join(" · ");
}
