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

/**
 * El token de mención que se está escribiendo justo antes del cursor, si hay
 * uno.
 *
 * Un disparador cuenta sólo al principio del texto o después de un espacio:
 * un «@» a mitad de palabra (un correo pegado, por ejemplo) no es una
 * mención. El término se corta en el primer espacio, así que no hace falta
 * que el usuario lo cierre para que deje de contar.
 */
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
 * Los chips cuyo texto sigue efectivamente presente en el borrador, en el
 * orden en que aparecen — y con su posición exacta, para que `Mensaje` los
 * pinte como chips sin volver a buscarlos.
 *
 * BUSCA CADA TEXTO DESDE DESPUÉS DEL ANTERIOR, nunca desde el principio: dos
 * menciones con el mismo texto (la misma materia mencionada dos veces) no
 * pueden las dos reclamar la primera aparición — cada chip es una elección
 * propia, y la segunda tiene que apuntar a la segunda aparición.
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
