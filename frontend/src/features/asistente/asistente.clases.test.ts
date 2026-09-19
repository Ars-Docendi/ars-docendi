/// <reference types="node" />
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, it, expect } from "vitest";

import { hojaDeLaFeature, sinComentarios } from "./test/hojas";

// ============================================================
// Ninguna clase de la hoja está huérfana.
//
// EL MODO DE FALLA ES EL OPUESTO AL DEL GUARD DE TOKENS. Aquél cuida que la hoja
// no use algo que no existe; éste cuida que no defina algo que nadie usa. Una
// regla muerta no rompe nada —por eso sobrevive— y a la vez es lo que hace que
// leer el CSS deje de decir la verdad sobre la pantalla: quien la lee cree que
// esa clase se aplica en algún lado y busca dónde.
//
// Se afirma sobre el texto SIN comentarios, igual que el guard de tokens: una
// regla comentada no está viva y tampoco cuenta como uso.
// ============================================================

const raiz = join(import.meta.dirname);

/**
 * El código de PRODUCCIÓN de la feature: `.ts` y `.tsx`, recursivo, sin tests.
 *
 * Los tests quedan afuera a propósito. Un test que nombra una clase la está
 * verificando, no aplicándola: contarlo como uso dejaría viva una regla que ya
 * no pinta nada mientras su test siga existiendo, que es exactamente la clase de
 * cosa que este guard busca.
 */
const codigoDeLaFeature = (directorio: string): string[] =>
  readdirSync(directorio).flatMap((entrada) => {
    const ruta = join(directorio, entrada);
    if (statSync(ruta).isDirectory()) {
      return codigoDeLaFeature(ruta);
    }
    return /\.tsx?$/.test(entrada) && !/\.test\.tsx?$/.test(entrada)
      ? [readFileSync(ruta, "utf8")]
      : [];
  });

const hoja = sinComentarios(hojaDeLaFeature());
const codigo = codigoDeLaFeature(raiz).join("\n");

describe("Las clases de la hoja del asistente", () => {
  it("se lee de verdad: hay hoja y hay código", () => {
    expect(hoja.length).toBeGreaterThan(0);
    expect(codigo.length).toBeGreaterThan(0);
  });

  it("no define ninguna clase propia que el código no use", () => {
    const definidas = new Set(
      [...hoja.matchAll(/\.(adoc-asistente[\w-]*)/g)].map((coincidencia) => coincidencia[1]),
    );

    // Se busca el nombre como texto y no como palabra suelta: las clases se
    // componen tanto en `className="adoc-x"` como en plantillas
    // (`` `adoc-x ${activo}` ``) y en `clsx`, y todas esas formas contienen el
    // nombre completo.
    const huerfanas = [...definidas].filter((clase) => !codigo.includes(clase)).sort();

    expect(huerfanas).toEqual([]);
  });

  it("sólo sobreescribe clases de la librería que están declaradas", () => {
    // LA OTRA MITAD. El guard de arriba mira las clases con el prefijo de la
    // feature; una clase `adoc-` SIN ese prefijo es de `@ars-docendi/ui` y la hoja
    // la está sobreescribiendo. Eso es legítimo —así se ajusta la tabla sin
    // `!important` ni fork— pero tiene que ser una decisión visible: una
    // sobreescritura que aparece sola es una feature pisando estilos ajenos.
    const declaradas = ["adoc-modal-stage", "adoc-table"];

    const ajenas = [
      ...new Set(
        [...hoja.matchAll(/\.(adoc-[\w-]+)/g)]
          .map((coincidencia) => coincidencia[1])
          .filter((clase) => !clase.startsWith("adoc-asistente")),
      ),
    ].sort();

    expect(ajenas).toEqual(declaradas);
  });

  it("detecta una clase huérfana", () => {
    // EL PAR SINTÉTICO. Sin esto, una expresión regular que dejara de matchear
    // pasaría en verde para siempre afirmando que no hay huérfanas porque no
    // encontró ninguna clase.
    const inventada = ".adoc-asistente-clase-que-nadie-usa { display: none; }";
    const definidas = [...sinComentarios(inventada).matchAll(/\.(adoc-[\w-]+)/g)].map(
      (coincidencia) => coincidencia[1],
    );

    expect(definidas).toEqual(["adoc-asistente-clase-que-nadie-usa"]);
    expect(definidas.filter((clase) => !codigo.includes(clase))).toEqual([
      "adoc-asistente-clase-que-nadie-usa",
    ]);
  });
});
