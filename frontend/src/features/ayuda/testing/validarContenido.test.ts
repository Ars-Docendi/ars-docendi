import { describe, expect, it } from "vitest";

import { ARTICULOS, MODULOS } from "./contenidoPrueba";
import type { ArticuloAyuda, ModuloAyuda } from "../tipos";
import { validarContenido } from "./validarContenido";

const PERMISOS_NAVEGACION = new Set([
  "tareas.ver",
  "tareas.gestionar",
  "designaciones.revisar",
  "periodos.administrar",
  "roles.ver",
]);

function validar(modulos: readonly ModuloAyuda[], articulos: readonly ArticuloAyuda[]) {
  return validarContenido({
    modulos,
    articulos,
    permisosNavegacion: PERMISOS_NAVEGACION,
    existeArchivo: () => true,
  });
}

function conArticulo(slug: string, cambios: Partial<ArticuloAyuda>): ArticuloAyuda[] {
  return ARTICULOS.map((a) => (a.slug === slug ? { ...a, ...cambios } : a));
}

describe("validarContenido", () => {
  it("el contenido de prueba válido no produce errores", () => {
    expect(validar(MODULOS, ARTICULOS)).toEqual([]);
  });

  it("detecta slugs de módulo repetidos", () => {
    expect(validar([...MODULOS, MODULOS[0]], ARTICULOS)).toContainEqual(
      expect.stringContaining("módulo repetido: general"),
    );
  });

  it("detecta slugs de artículo repetidos dentro de un módulo", () => {
    expect(validar(MODULOS, [...ARTICULOS, ARTICULOS[0]])).toContainEqual(
      expect.stringContaining("artículo repetido: general/bienvenida"),
    );
  });

  it("exige kebab-case y reserva `buscar`", () => {
    const errores = validar(
      [
        { ...MODULOS[0], slug: "Mal Slug" },
        { ...MODULOS[1], slug: "buscar" },
      ],
      [],
    );
    expect(errores).toContainEqual(expect.stringContaining("kebab-case: Mal Slug"));
    expect(errores).toContainEqual(expect.stringContaining("slug reservado: buscar"));
    expect(validar(MODULOS, conArticulo("atajos", { slug: "Atajos_1" }))).toContainEqual(
      expect.stringContaining("kebab-case: Atajos_1"),
    );
  });

  it("detecta un módulo inexistente", () => {
    expect(validar(MODULOS, conArticulo("atajos", { moduloSlug: "fantasma" }))).toContainEqual(
      expect.stringContaining("atajos: módulo inexistente fantasma"),
    );
  });

  it("detecta un grupo inexistente", () => {
    expect(validar(MODULOS, conArticulo("atajos", { grupo: "Otro" }))).toContainEqual(
      expect.stringContaining("atajos: grupo inexistente Otro"),
    );
  });

  it("detecta un relacionado roto indicando artículo y referencia", () => {
    const errores = validar(
      MODULOS,
      conArticulo("atajos", { relacionados: ["tareas/inexistente"] }),
    );
    expect(errores).toContainEqual(
      expect.stringContaining("general/atajos: relacionado inexistente tareas/inexistente"),
    );
  });

  it("detecta una captura sin alt", () => {
    const pasos = [{ titulo: "P", cuerpo: "C", captura: { src: "/ayuda/x.webp", alt: "  " } }];
    expect(validar(MODULOS, conArticulo("atajos", { pasos }))).toContainEqual(
      expect.stringContaining("captura sin alt"),
    );
  });

  it("detecta un archivo de captura inexistente", () => {
    const errores = validarContenido({
      modulos: MODULOS,
      articulos: ARTICULOS,
      permisosNavegacion: PERMISOS_NAVEGACION,
      existeArchivo: () => false,
    });
    expect(errores).toContainEqual(
      expect.stringContaining(
        "archivo de captura inexistente: /ayuda/general/bienvenida-paso-1.webp",
      ),
    );
  });

  it("detecta un permiso que no figura en la navegación", () => {
    expect(
      validar(MODULOS, conArticulo("ver-tareas", { permisos: ["tareas.volar"] })),
    ).toContainEqual(expect.stringContaining("permiso inexistente en la navegación: tareas.volar"));
  });

  it("detecta que el módulo no cubre los permisos de sus artículos", () => {
    const modulos = MODULOS.map((m) =>
      m.slug === "tareas" ? { ...m, permisos: ["tareas.ver"] } : m,
    );
    expect(validar(modulos, ARTICULOS)).toContainEqual(
      expect.stringContaining("módulo tareas no cubre los permisos de sus artículos"),
    );
  });

  it("un artículo abierto exige un módulo abierto", () => {
    const modulos = MODULOS.map((m) =>
      m.slug === "general" ? { ...m, permisos: ["roles.ver"] } : m,
    );
    expect(validar(modulos, ARTICULOS)).toContainEqual(
      expect.stringContaining("módulo general no cubre los permisos de sus artículos"),
    );
  });

  it("detecta un artículo sin pasos", () => {
    expect(validar(MODULOS, conArticulo("atajos", { pasos: [] }))).toContainEqual(
      expect.stringContaining("general/atajos: sin pasos"),
    );
  });
});
