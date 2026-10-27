import type { ChipDeMencion, MencionEnPregunta, TipoDeMencion } from "../types";

/** El disparador y lo que se lleva escrito de él, en la posición del cursor. */
export interface TokenDeMencion {
  disparador: "@" | "#";
  termino: string;
  /** Dónde empieza el disparador dentro del texto completo. */
  inicio: number;
  /** El cursor: donde termina el término tecleado hasta ahora. */
  fin: number;
}

/** A qué tipo de mención corresponde cada disparador. Es 1 a 1, no hay un tercero. */
export function tipoDelDisparador(disparador: "@" | "#"): TipoDeMencion {
  return disparador === "@" ? "materia" : "docente";
}

/** El token de mención que se está escribiendo justo antes del cursor, si hay uno. */
export function detectarToken(valor: string, cursor: number): TokenDeMencion | null {
  const antes = valor.slice(0, cursor);
  const coincidencia = /(^|\s)([@#])(\S*)$/.exec(antes);
  if (!coincidencia) return null;

  const [, previo, disparador, termino] = coincidencia;
  return {
    disparador: disparador as "@" | "#",
    termino,
    inicio: coincidencia.index + previo.length,
    fin: cursor,
  };
}

/** El texto insertado en el campo al elegir una mención: disparador + nombre. */
export function textoDeLaMencion(disparador: "@" | "#", nombre: string): string {
  return `${disparador}${nombre}`;
}

/**
 * Los chips cuyo texto sigue efectivamente presente en el borrador, en el orden en que aparecen —
 * y con su posición exacta, para que `Mensaje` los pinte como chips sin volver a buscarlos.
 */
export function ubicarMenciones(chips: ChipDeMencion[], texto: string): MencionEnPregunta[] {
  const ubicadas: MencionEnPregunta[] = [];
  let desde = 0;

  for (const chip of chips) {
    const inicio = texto.indexOf(chip.texto, desde);
    if (inicio === -1) continue;

    const fin = inicio + chip.texto.length;
    ubicadas.push({ tipo: chip.tipo, id: chip.id, texto: chip.texto, inicio, fin });
    desde = fin;
  }

  return ubicadas;
}
