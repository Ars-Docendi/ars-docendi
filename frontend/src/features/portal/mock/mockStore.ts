// ============================================================
// Store mock del Portal del docente. Estado en memoria, sin backend.
//
// Convención del proyecto (misma que features/docentes): seeds + helpers puros
// que reciben una lista y devuelven una lista nueva. El estado vive en la
// página con useState.
//
// TODO(backend): reemplazar por Modules.Portal vía React Query.
// ============================================================

import { PERSONAS_SISTEMA } from "../../../shared/mock/personasSistema";

export { perfilDe } from "./perfilesSeed";
import type {
  Certificacion,
  DatosCertificacion,
  DatosEducacion,
  DatosExperiencia,
  DatosProyecto,
  Educacion,
  Experiencia,
  PerfilInstitucional,
  Proyecto,
  Tag,
} from "../types";

/**
 * Vocabulario curado de habilidades e intereses. Ambas listas comparten
 * vocabulario; el docente puede sugerir términos que no estén acá.
 *
 * OQ: catálogo curado vs texto libre sigue abierto — ver el design spec.
 */
export const VOCABULARIO_EXPERTICIA: string[] = [
  "Algoritmos",
  "Arquitectura de software",
  "Bases de datos",
  "Ciberseguridad",
  "Ciencia de datos",
  "Cloud computing",
  "Computación gráfica",
  "Desarrollo web",
  "DevOps",
  "Machine learning",
  "Matemática discreta",
  "Programación orientada a objetos",
  "Redes de computadoras",
  "Robótica",
  "Sistemas embebidos",
  "Sistemas operativos",
  "Testing y calidad",
];

/**
 * Datos de solo lectura del perfil: identidad de Azure AD + datos
 * institucionales de Secretaría, buscados por UPN en el padrón compartido.
 * El teléfono del padrón NO se lee: el de contacto lo mantiene el docente.
 */
export function obtenerPerfilInstitucional(upn: string): PerfilInstitucional | null {
  const persona = PERSONAS_SISTEMA.find((p) => p.upn === upn);
  if (!persona) return null;
  return {
    nombre: persona.nombre,
    apellido: persona.apellido,
    upn: persona.upn,
    documento: persona.documento,
    legajo: persona.legajo,
    cuil: persona.cuil,
  };
}

// ------------------------------------------------------------
// Helpers de listas — reciben una lista y devuelven una nueva.
// ------------------------------------------------------------

export function agregarExperiencia(lista: Experiencia[], datos: DatosExperiencia): Experiencia[] {
  return [...lista, { ...datos, id: crypto.randomUUID() }];
}

export function editarExperiencia(
  lista: Experiencia[],
  id: string,
  datos: DatosExperiencia,
): Experiencia[] {
  return lista.map((e) => (e.id === id ? { ...datos, id } : e));
}

export function agregarEducacion(lista: Educacion[], datos: DatosEducacion): Educacion[] {
  return [...lista, { ...datos, id: crypto.randomUUID() }];
}

export function editarEducacion(
  lista: Educacion[],
  id: string,
  datos: DatosEducacion,
): Educacion[] {
  return lista.map((e) => (e.id === id ? { ...datos, id } : e));
}

export function agregarCertificacion(
  lista: Certificacion[],
  datos: DatosCertificacion,
): Certificacion[] {
  return [...lista, { ...datos, id: crypto.randomUUID() }];
}

export function editarCertificacion(
  lista: Certificacion[],
  id: string,
  datos: DatosCertificacion,
): Certificacion[] {
  return lista.map((c) => (c.id === id ? { ...datos, id } : c));
}

export function agregarProyecto(lista: Proyecto[], datos: DatosProyecto): Proyecto[] {
  return [...lista, { ...datos, id: crypto.randomUUID() }];
}

export function editarProyecto(lista: Proyecto[], id: string, datos: DatosProyecto): Proyecto[] {
  return lista.map((p) => (p.id === id ? { ...datos, id } : p));
}

/** Borrado genérico por id, común a todas las listas del perfil. */
export function eliminarPorId<T extends { id: string }>(lista: T[], id: string): T[] {
  return lista.filter((item) => item.id !== id);
}

// ------------------------------------------------------------
// Tags — habilidades e intereses comparten mecánica y vocabulario.
// ------------------------------------------------------------

/** Agrega un término si no estaba. Marca `sugerido` si no está en el vocabulario. */
export function agregarTag(lista: Tag[], termino: string): Tag[] {
  const limpio = termino.trim();
  if (!limpio) return lista;
  const yaEsta = lista.some((t) => t.termino.toLowerCase() === limpio.toLowerCase());
  if (yaEsta) return lista;
  const enVocabulario = VOCABULARIO_EXPERTICIA.some(
    (v) => v.toLowerCase() === limpio.toLowerCase(),
  );
  return [...lista, { termino: limpio, sugerido: !enVocabulario }];
}

export function quitarTag(lista: Tag[], termino: string): Tag[] {
  return lista.filter((t) => t.termino !== termino);
}

/** Términos del vocabulario que el docente todavía no eligió en esa lista. */
export function vocabularioDisponible(lista: Tag[]): string[] {
  const elegidos = new Set(lista.map((t) => t.termino.toLowerCase()));
  return VOCABULARIO_EXPERTICIA.filter((v) => !elegidos.has(v.toLowerCase()));
}
