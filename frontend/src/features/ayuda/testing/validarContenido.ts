import { SLUG_RESERVADO } from "../utils/rutas";
import type { ArticuloAyuda, ModuloAyuda } from "../tipos";

const KEBAB_CASE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export interface EntradaValidacion {
  modulos: readonly ModuloAyuda[];
  articulos: readonly ArticuloAyuda[];
  /** Permisos que declara `app/shell/nav.ts`. */
  permisosNavegacion: ReadonlySet<string>;
  /** Indica si el archivo de una captura (`/ayuda/...`) existe bajo `frontend/public`. */
  existeArchivo: (src: string) => boolean;
}

/** Devuelve la lista de violaciones de integridad del contenido (vacía si es válido). */
export function validarContenido(entrada: EntradaValidacion): string[] {
  const { modulos, articulos, permisosNavegacion, existeArchivo } = entrada;
  const errores: string[] = [];
  const modulosPorSlug = new Map(modulos.map((modulo) => [modulo.slug, modulo]));

  const vistos = new Set<string>();
  for (const modulo of modulos) {
    if (vistos.has(modulo.slug)) errores.push(`módulo repetido: ${modulo.slug}`);
    vistos.add(modulo.slug);
    if (!KEBAB_CASE.test(modulo.slug))
      errores.push(`slug de módulo no es kebab-case: ${modulo.slug}`);
    if (modulo.slug === SLUG_RESERVADO) errores.push(`slug reservado: ${modulo.slug}`);
    for (const permiso of modulo.permisos) {
      if (!permisosNavegacion.has(permiso)) {
        errores.push(`módulo ${modulo.slug}: permiso inexistente en la navegación: ${permiso}`);
      }
    }
  }

  const referencias = new Set<string>();
  for (const articulo of articulos) {
    const ref = `${articulo.moduloSlug}/${articulo.slug}`;
    if (referencias.has(ref)) errores.push(`artículo repetido: ${ref}`);
    referencias.add(ref);
  }

  for (const articulo of articulos) {
    const ref = `${articulo.moduloSlug}/${articulo.slug}`;
    if (!KEBAB_CASE.test(articulo.slug)) {
      errores.push(`slug de artículo no es kebab-case: ${articulo.slug}`);
    }
    const modulo = modulosPorSlug.get(articulo.moduloSlug);
    if (!modulo) errores.push(`${articulo.slug}: módulo inexistente ${articulo.moduloSlug}`);
    else if (!modulo.grupos.includes(articulo.grupo)) {
      errores.push(`${articulo.slug}: grupo inexistente ${articulo.grupo}`);
    }
    for (const permiso of articulo.permisos) {
      if (!permisosNavegacion.has(permiso)) {
        errores.push(`${ref}: permiso inexistente en la navegación: ${permiso}`);
      }
    }
    for (const relacionado of articulo.relacionados) {
      if (!referencias.has(relacionado)) {
        errores.push(`${ref}: relacionado inexistente ${relacionado}`);
      }
    }
    if (articulo.pasos.length === 0) errores.push(`${ref}: sin pasos`);
    articulo.pasos.forEach((paso, indice) => {
      if (!paso.captura) return;
      const { src, alt } = paso.captura;
      if (alt.trim() === "") errores.push(`${ref} paso ${indice + 1}: captura sin alt`);
      if (!existeArchivo(src)) {
        errores.push(`${ref} paso ${indice + 1}: archivo de captura inexistente: ${src}`);
      }
    });
  }

  for (const modulo of modulos) {
    if (modulo.permisos.length === 0) continue;
    const delModulo = articulos.filter((articulo) => articulo.moduloSlug === modulo.slug);
    const cubre = delModulo.every(
      (articulo) =>
        articulo.permisos.length > 0 &&
        articulo.permisos.every((permiso) => modulo.permisos.includes(permiso)),
    );
    if (!cubre) errores.push(`módulo ${modulo.slug} no cubre los permisos de sus artículos`);
  }

  return errores;
}
