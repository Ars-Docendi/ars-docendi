import { describe, expect, it } from "vitest";

import { NAVEGACION } from "../../../app/shell/nav";
import { ARTICULOS, MODULOS } from "./index";
import { validarContenido } from "../testing/validarContenido";

// Solo se usan las claves del glob (los archivos existentes), no se cargan.
const archivosPublicos = Object.keys(import.meta.glob("../../../../public/ayuda/**/*"));

function existeEnPublic(src: string): boolean {
  return archivosPublicos.some((ruta) => ruta.endsWith(`/public${src}`));
}

describe("integridad del contenido de ayuda", () => {
  it("el registro real cumple todas las reglas", () => {
    const errores = validarContenido({
      modulos: MODULOS,
      articulos: ARTICULOS,
      permisosNavegacion: new Set(
        NAVEGACION.flatMap((grupo) => grupo.items.flatMap((i) => [i, ...(i.children ?? [])])).map(
          (item) => item.permiso,
        ),
      ),
      existeArchivo: existeEnPublic,
    });
    expect(errores).toEqual([]);
  });

  it("declara los cinco módulos documentados y ninguno de Aulas ni del Asistente", () => {
    expect(MODULOS.map((m) => m.slug)).toEqual([
      "primeros-pasos",
      "mi-portal",
      "tareas",
      "designaciones",
      "administracion",
    ]);
  });
});
