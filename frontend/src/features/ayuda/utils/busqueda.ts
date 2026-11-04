import type { ArticuloAyuda } from "../tipos";

const PESO_TITULO = 10;
const PESO_PALABRAS_CLAVE = 6;
const PESO_RESUMEN = 3;
const PESO_CUERPO = 1;

const STOP_WORDS: ReadonlySet<string> = new Set([
  "el",
  "la",
  "los",
  "las",
  "un",
  "una",
  "de",
  "del",
  "en",
  "y",
  "o",
  "a",
  "al",
  "por",
  "para",
  "con",
  "que",
  "como",
  "se",
  "mi",
  "tu",
  "su",
]);

interface TextoIndexado {
  titulo: string;
  palabrasClave: string;
  resumen: string;
  cuerpo: string;
}

/** Minúsculas y sin marcas diacríticas ("Períodos" -> "periodos"). */
export function normalizar(texto: string): string {
  return texto.toLowerCase().normalize("NFD").replace(/\p{M}/gu, "");
}

/** Términos de la consulta: normalizados, sin puntuación ni stop-words. */
export function terminos(consulta: string): string[] {
  return normalizar(consulta)
    .split(/[^\p{L}\p{N}]+/u)
    .filter((termino) => termino !== "" && !STOP_WORDS.has(termino));
}

// El texto normalizado no depende de la sesión: se calcula una vez por artículo.
const indice = new WeakMap<ArticuloAyuda, TextoIndexado>();

function indexar(articulo: ArticuloAyuda): TextoIndexado {
  const existente = indice.get(articulo);
  if (existente) return existente;
  const cuerpo = [
    articulo.introduccion,
    ...articulo.pasos.flatMap((paso) => [paso.titulo, paso.cuerpo, paso.nota?.cuerpo ?? ""]),
    articulo.despues?.cuerpo ?? "",
    ...(articulo.despues?.viñetas ?? []).flatMap((v) => [v.termino, v.descripcion]),
    ...(articulo.preguntas ?? []).flatMap((p) => [p.pregunta, p.respuesta]),
  ].join(" ");
  const nuevo: TextoIndexado = {
    titulo: normalizar(articulo.titulo),
    palabrasClave: normalizar((articulo.palabrasClave ?? []).join(" ")),
    resumen: normalizar(articulo.resumen),
    cuerpo: normalizar(cuerpo),
  };
  indice.set(articulo, nuevo);
  return nuevo;
}

function puntajeTermino(texto: TextoIndexado, termino: string): number {
  return (
    (texto.titulo.includes(termino) ? PESO_TITULO : 0) +
    (texto.palabrasClave.includes(termino) ? PESO_PALABRAS_CLAVE : 0) +
    (texto.resumen.includes(termino) ? PESO_RESUMEN : 0) +
    (texto.cuerpo.includes(termino) ? PESO_CUERPO : 0)
  );
}

/**
 * Busca sobre los artículos recibidos (el llamador ya los filtró por
 * visibilidad). Todos los términos deben aparecer; se ordena por puntaje y, en
 * empate, por el orden recibido.
 */
export function buscar(consulta: string, articulos: readonly ArticuloAyuda[]): ArticuloAyuda[] {
  const buscados = terminos(consulta);
  if (buscados.length === 0) return [];
  return articulos
    .map((articulo, posicion) => {
      const texto = indexar(articulo);
      const puntajes = buscados.map((termino) => puntajeTermino(texto, termino));
      return {
        articulo,
        posicion,
        puntaje: puntajes.reduce((suma, p) => suma + p, 0),
        coincide: puntajes.every((p) => p > 0),
      };
    })
    .filter((resultado) => resultado.coincide)
    .sort((a, b) => b.puntaje - a.puntaje || a.posicion - b.posicion)
    .map((resultado) => resultado.articulo);
}
