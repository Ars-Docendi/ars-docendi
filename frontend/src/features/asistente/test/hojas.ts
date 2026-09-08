/// <reference types="node" />
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

/**
 * Las hojas de estilo de la feature, leídas como TEXTO.
 *
 * Barre el DIRECTORIO en vez de nombrar `asistente.css`. La diferencia importa: un
 * `.css` nuevo con un token inexistente o un color a mano no rompe nada visible
 * —`var(--token, fallback)` se renderiza siempre—, y con la lista escrita a mano
 * ese archivo nunca entraría al guard. Barriendo, entra solo.
 *
 * Va por `fs` y no por `?raw`: con `css: false` en la config, vitest resuelve
 * cualquier import de un `.css` —también con `?raw`— a una cadena vacía, y este
 * guard pasó un tiempo afirmando cosas sobre dos cadenas vacías.
 */
const directorio = join(import.meta.dirname, "..");

export const hojasDeLaFeature = (): string[] =>
  readdirSync(directorio)
    .filter((archivo) => archivo.endsWith(".css"))
    .map((archivo) => readFileSync(join(directorio, archivo), "utf8"));

/** Las hojas concatenadas, que es como las miran los guards textuales. */
export const hojaDeLaFeature = (): string => hojasDeLaFeature().join("\n");

/** El texto sin comentarios: una regla comentada no está viva. */
export const sinComentarios = (css: string): string => css.replace(/\/\*[\s\S]*?\*\//g, "");
