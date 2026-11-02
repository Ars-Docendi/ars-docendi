import { describe, expect, it } from "vitest";

import { ARTICULOS } from "../testing/contenidoPrueba";
import type { ArticuloAyuda } from "../tipos";
import { buscar, normalizar, terminos } from "./busqueda";

function articulo(parcial: Partial<ArticuloAyuda> & Pick<ArticuloAyuda, "slug">): ArticuloAyuda {
  return {
    moduloSlug: "m",
    grupo: "g",
    titulo: "Título",
    resumen: "Resumen",
    introduccion: "Introducción",
    permisos: [],
    pasos: [{ titulo: "Paso", cuerpo: "Cuerpo" }],
    relacionados: [],
    ...parcial,
  };
}

const slugs = (resultado: readonly ArticuloAyuda[]) => resultado.map((a) => a.slug);

describe("normalizar y terminos", () => {
  it("pasa a minúsculas y quita acentos", () => {
    expect(normalizar("PERÍODOS de Designación")).toBe("periodos de designacion");
  });

  it("descarta stop-words y puntuación", () => {
    expect(terminos("¿Cómo crear un pedido?")).toEqual(["crear", "pedido"]);
  });

  it("una consulta solo de stop-words no tiene términos", () => {
    expect(terminos("el de la")).toEqual([]);
    expect(terminos("   ")).toEqual([]);
  });
});

describe("buscar", () => {
  it("ignora acentos y mayúsculas", () => {
    expect(slugs(buscar("PERIODOS", ARTICULOS))).toEqual(["gestionar-periodos"]);
  });

  it("exige todos los términos (AND) como subcadena", () => {
    const articulos = [
      articulo({ slug: "solo-pedido", introduccion: "Un pedido." }),
      articulo({ slug: "pedido-adjunto", introduccion: "Un pedido con adjunto." }),
    ];
    expect(slugs(buscar("pedido adjunto", articulos))).toEqual(["pedido-adjunto"]);
    expect(slugs(buscar("adjun", articulos))).toEqual(["pedido-adjunto"]);
  });

  it("las stop-words no cambian el resultado", () => {
    const articulos = [articulo({ slug: "a", titulo: "Crear un pedido" })];
    expect(slugs(buscar("como crear un pedido", articulos))).toEqual(
      slugs(buscar("crear pedido", articulos)),
    );
    expect(slugs(buscar("crear pedido", articulos))).toEqual(["a"]);
  });

  it("el título pesa más que palabras clave, resumen y cuerpo", () => {
    const articulos = [
      articulo({ slug: "cuerpo", introduccion: "Habla de revisión." }),
      articulo({ slug: "resumen", resumen: "Sobre la revisión." }),
      articulo({ slug: "clave", palabrasClave: ["revisión"] }),
      articulo({ slug: "titulo", titulo: "Revisión de pedidos" }),
    ];
    expect(slugs(buscar("revision", articulos))).toEqual(["titulo", "clave", "resumen", "cuerpo"]);
  });

  it("busca en pasos y preguntas frecuentes como cuerpo", () => {
    const articulos = [
      articulo({ slug: "en-paso", pasos: [{ titulo: "Paso", cuerpo: "Mencionamos un gizmo." }] }),
      articulo({
        slug: "en-pregunta",
        preguntas: [{ pregunta: "¿Y el gizmo?", respuesta: "Sí." }],
      }),
      articulo({ slug: "ninguno" }),
    ];
    expect(slugs(buscar("gizmo", articulos))).toEqual(["en-paso", "en-pregunta"]);
  });

  it("busca en las viñetas de «¿Qué sucede después?» como cuerpo", () => {
    const articulos = [
      articulo({
        slug: "en-termino",
        despues: {
          titulo: "Después",
          cuerpo: "",
          viñetas: [{ termino: "Gizmo", descripcion: "" }],
        },
      }),
      articulo({
        slug: "en-descripcion",
        despues: {
          titulo: "Después",
          cuerpo: "",
          viñetas: [{ termino: "Estado", descripcion: "Podés reabrir el gizmo." }],
        },
      }),
      articulo({ slug: "ninguno" }),
    ];
    expect(slugs(buscar("gizmo", articulos))).toEqual(["en-termino", "en-descripcion"]);
  });

  it("en empate conserva el orden del contenido", () => {
    const articulos = ["c", "a", "b"].map((slug) => articulo({ slug, titulo: "Igual" }));
    expect(slugs(buscar("igual", articulos))).toEqual(["c", "a", "b"]);
  });

  it("suma el puntaje de cada término", () => {
    const articulos = [
      articulo({ slug: "uno", titulo: "Tablero" }),
      articulo({ slug: "dos", titulo: "Tablero de pedidos" }),
    ];
    expect(slugs(buscar("tablero pedidos", articulos))).toEqual(["dos"]);
    expect(slugs(buscar("tablero", articulos))).toEqual(["uno", "dos"]);
  });

  it("sin términos o sin artículos no hay resultados", () => {
    expect(buscar("", ARTICULOS)).toEqual([]);
    expect(buscar("de la", ARTICULOS)).toEqual([]);
    expect(buscar("tareas", [])).toEqual([]);
  });

  it("no incluye artículos fuera de la lista recibida (p. ej. no visibles)", () => {
    const visibles = ARTICULOS.filter((a) => a.moduloSlug !== "administracion");
    expect(buscar("permisos granulares", visibles)).toEqual([]);
    expect(slugs(buscar("permisos granulares", ARTICULOS))).toEqual(["gestionar-roles"]);
  });
});
