/// <reference types="node" />
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

/** Las hojas de estilo de la feature, leídas como TEXTO. */
const directorio = join(import.meta.dirname, "..");

export const hojasDeLaFeature = (): string[] =>
  readdirSync(directorio)
    .filter((archivo) => archivo.endsWith(".css"))
    .map((archivo) => readFileSync(join(directorio, archivo), "utf8"));

/** Las hojas concatenadas, que es como las miran los guards textuales. */
export const hojaDeLaFeature = (): string => hojasDeLaFeature().join("\n");

/** El texto sin comentarios: una regla comentada no está viva. */
export const sinComentarios = (css: string): string => css.replace(/\/\*[\s\S]*?\*\//g, "");
