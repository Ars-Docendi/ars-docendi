import { CELDA_VACIA, formatearCelda } from "./celdas";

/** Los dos sentidos de un orden; sin un tercero: activar la misma columna alterna
 * entre estos dos, nunca vuelve al orden original (asistente-tabla-de-resultado). */
export type DireccionDeOrden = "ascendente" | "descendente";

/** El estado de orden de una tabla: qué columna, en qué sentido. */
export interface OrdenDeColumna {
  columna: number;
  direccion: DireccionDeOrden;
}

type TipoDeColumna = "numero" | "fecha" | "texto";

const ES_NUMERO = /^-?\d+(\.\d+)?$/;

// yyyy-mm-dd, opcionalmente con hora, segundos, fracción y zona.
const ES_FECHA_ISO = /^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?)?$/;

// `numeric: true` compara números embebidos numéricamente («Materia 9» antes que
// «Materia 10»); `sensitivity: "base"` ignora mayúsculas y acentos — exactamente
// lo que pide la spec para el texto en español.
const COLACIONADOR_ES = new Intl.Collator("es", { numeric: true, sensitivity: "base" });

function tipoDeColumna(valoresMostrados: string[]): TipoDeColumna {
  const noVacios = valoresMostrados.filter((valor) => valor !== CELDA_VACIA);
  if (noVacios.length === 0) return "texto";
  if (noVacios.every((valor) => ES_NUMERO.test(valor))) return "numero";
  if (noVacios.every((valor) => ES_FECHA_ISO.test(valor))) return "fecha";
  return "texto";
}

function claveDeOrden(valorMostrado: string, tipo: TipoDeColumna): number | string | null {
  if (valorMostrado === CELDA_VACIA) return null;
  if (tipo === "numero") return Number(valorMostrado);
  if (tipo === "fecha") return new Date(valorMostrado).getTime();
  return valorMostrado;
}

/**
 * El orden en que se muestran las filas: los índices ORIGINALES de `filas`,
 * nunca una copia reordenada de `filas` en sí.
 *
 * ES SOBRE ÍNDICES Y NO SOBRE VALORES a propósito (design.md D5 de
 * asistente-rediseno-v3): `vinculos` está indexado por `fila:columna` con el
 * índice ORIGINAL, y la exportación necesita releer la fila real detrás de
 * cada posición mostrada. Devolver un `unknown[][]` reordenado rompería esa
 * correspondencia en el llamador.
 *
 * `orden === null` devuelve el orden original (0..n-1): es el estado inicial,
 * antes de que el usuario active un encabezado.
 *
 * EL TIPO DE LA COLUMNA SE INFIERE DE LO MOSTRADO, nunca del valor
 * subyacente: `formatearCelda` es la misma función que pinta la celda, así
 * que una columna enmascarada ordena por su máscara y no por lo que el
 * cliente no tiene. Por eso esta función no recibe `columnas`: el tipo se
 * infiere de los valores de la propia columna, no de sus metadatos.
 */
export function ordenarFilas(filas: unknown[][], orden: OrdenDeColumna | null): number[] {
  const indicesOriginales = filas.map((_, indice) => indice);
  if (!orden) return indicesOriginales;

  const { columna, direccion } = orden;
  const valoresMostrados = filas.map((fila) => formatearCelda(fila[columna]));
  const tipo = tipoDeColumna(valoresMostrados);
  const claves = valoresMostrados.map((valor) => claveDeOrden(valor, tipo));
  const signo = direccion === "ascendente" ? 1 : -1;

  // Copia: `Array.prototype.sort` muta, y `indicesOriginales` es lo que se
  // devuelve como «el orden original» cuando `orden` es `null` más arriba —
  // no puede ser el mismo array que este `sort` reordena.
  return [...indicesOriginales].sort((a, b) => {
    const claveA = claves[a];
    const claveB = claves[b];

    // Vacíos siempre al final, EN CUALQUIER DIRECCIÓN: no se multiplica por
    // `signo`. Entre dos vacíos, el orden original (estable).
    if (claveA === null && claveB === null) return a - b;
    if (claveA === null) return 1;
    if (claveB === null) return -1;

    const comparacion =
      tipo === "texto"
        ? COLACIONADOR_ES.compare(claveA as string, claveB as string)
        : (claveA as number) - (claveB as number);

    // Estable: entre claves iguales, el orden original — nunca al revés con
    // `signo`, o "estable" dependería de la dirección.
    return comparacion !== 0 ? signo * comparacion : a - b;
  });
}
