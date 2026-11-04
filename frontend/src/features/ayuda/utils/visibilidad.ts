import { CONTENIDO } from "../contenido";
import type { ArticuloAyuda, ContenidoAyuda, ModuloAyuda } from "../tipos";

/** Cualquiera de los permisos requeridos alcanza; una lista vacía es visible para todos. */
export function puedeVer(
  requeridos: readonly string[],
  permisosSesion: readonly string[],
): boolean {
  if (requeridos.length === 0) return true;
  const sesion = new Set(permisosSesion);
  return requeridos.some((permiso) => sesion.has(permiso));
}

function ordenGrupo(modulo: ModuloAyuda, articulo: ArticuloAyuda): number {
  const indice = modulo.grupos.indexOf(articulo.grupo);
  return indice === -1 ? modulo.grupos.length : indice;
}

/**
 * Artículos que la persona puede ver, ordenados por módulo, grupo y orden de
 * declaración. Es la única fuente para páginas, sidebar y búsqueda: nadie
 * consulta el registro crudo.
 */
export function articulosVisibles(
  permisos: readonly string[],
  contenido: ContenidoAyuda = CONTENIDO,
): ArticuloAyuda[] {
  const resultado: ArticuloAyuda[] = [];
  for (const modulo of contenido.modulos) {
    if (!puedeVer(modulo.permisos, permisos)) continue;
    contenido.articulos
      .filter((articulo) => articulo.moduloSlug === modulo.slug)
      .map((articulo, posicion) => ({ articulo, posicion }))
      .filter(({ articulo }) => puedeVer(articulo.permisos, permisos))
      .sort(
        (a, b) =>
          ordenGrupo(modulo, a.articulo) - ordenGrupo(modulo, b.articulo) ||
          a.posicion - b.posicion,
      )
      .forEach(({ articulo }) => resultado.push(articulo));
  }
  return resultado;
}

/** Módulos visibles con al menos un artículo visible. */
export function modulosVisibles(
  permisos: readonly string[],
  contenido: ContenidoAyuda = CONTENIDO,
): ModuloAyuda[] {
  const conArticulos = new Set(
    articulosVisibles(permisos, contenido).map((articulo) => articulo.moduloSlug),
  );
  return contenido.modulos.filter((modulo) => conArticulos.has(modulo.slug));
}

/**
 * Resuelve un artículo por URL. `undefined` si el módulo o el artículo no
 * existen, si el artículo no pertenece al módulo o si la persona no lo puede
 * ver (las páginas redirigen igual en los cuatro casos).
 */
export function articuloVisible(
  moduloSlug: string | undefined,
  articuloSlug: string | undefined,
  permisos: readonly string[],
  contenido: ContenidoAyuda = CONTENIDO,
): ArticuloAyuda | undefined {
  return articulosVisibles(permisos, contenido).find(
    (articulo) => articulo.moduloSlug === moduloSlug && articulo.slug === articuloSlug,
  );
}
