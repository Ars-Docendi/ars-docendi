import { claveDiaEnZona, tituloDiaEnZona, ZONA_INSTITUCION } from "./zonaHoraria";

export interface GrupoPorDia<T> {
  clave: string;
  titulo: string;
  elementos: T[];
}

/**
 * Agrupa una lista YA ORDENADA de más nuevo a más viejo por día calendario en
 * la zona de la institución (design D11). El orden de los grupos sale del
 * orden de inserción del `Map`, que respeta el orden de `elementos` — no hace
 * falta ordenar de nuevo.
 */
export function agruparPorDia<T>(
  elementos: readonly T[],
  obtenerFechaIso: (item: T) => string,
  ahora: Date = new Date(),
  zona: string = ZONA_INSTITUCION,
): GrupoPorDia<T>[] {
  const grupos = new Map<string, GrupoPorDia<T>>();
  for (const item of elementos) {
    const fecha = new Date(obtenerFechaIso(item));
    const clave = claveDiaEnZona(fecha, zona);
    let grupo = grupos.get(clave);
    if (!grupo) {
      grupo = { clave, titulo: tituloDiaEnZona(fecha, ahora, zona), elementos: [] };
      grupos.set(clave, grupo);
    }
    grupo.elementos.push(item);
  }
  return [...grupos.values()];
}
