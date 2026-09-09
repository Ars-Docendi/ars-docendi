import { describe, expect, it } from "vitest";

import type { UsuarioMock } from "./models";
import {
  aplicarFiltrosUsuarios,
  aplicarFiltrosYOrdenUsuarios,
  siguienteOrdenUsuarios,
  type FiltrosUsuarios,
} from "./filtrosUsuarios";

const base = (cambios: Partial<UsuarioMock>): UsuarioMock => ({
  id: "id",
  nombre: "Ana",
  apellido: "López",
  documento: "10",
  legajo: "10",
  cuil: "",
  fecha_nacimiento: "",
  telefono: "",
  upn: "ana@unlam.edu.ar",
  is_active: true,
  roles: ["Docente"],
  membresias: [],
  persona_id: "persona",
  perfilDocente: { esDocente: true, cantidadMaterias: 1 },
  ...cambios,
});

const vacios: FiltrosUsuarios = {
  apellidoNombre: "",
  documento: "",
  legajo: "",
  upn: "",
  roles: [],
  perfilDocente: [],
  estado: [],
};

describe("filtrosUsuarios", () => {
  it("combina búsqueda de nombre, roles, perfil y estado con AND", () => {
    const usuarios = [
      base({ id: "1", apellido: "López", roles: ["Docente"], is_active: true }),
      base({ id: "2", apellido: "López", roles: ["Administrativo"], is_active: true }),
      base({ id: "3", apellido: "Gómez", roles: ["Docente"], is_active: false }),
    ];

    expect(
      aplicarFiltrosUsuarios(usuarios, {
        ...vacios,
        apellidoNombre: "lopez",
        roles: ["Docente"],
        perfilDocente: ["si"],
        estado: ["activo"],
      }).map((usuario) => usuario.id),
    ).toEqual(["1"]);
  });

  it("ordena legajos numéricos y completa el ciclo de orden", () => {
    const usuarios = [
      base({ id: "1", legajo: "10" }),
      base({ id: "2", legajo: "2" }),
      base({ id: "3", legajo: "" }),
    ];

    expect(
      aplicarFiltrosYOrdenUsuarios(usuarios, vacios, { columna: "legajo", direccion: "asc" }).map(
        (usuario) => usuario.id,
      ),
    ).toEqual(["2", "1", "3"]);
    expect(
      aplicarFiltrosYOrdenUsuarios(usuarios, vacios, { columna: "legajo", direccion: "desc" }).map(
        (usuario) => usuario.id,
      ),
    ).toEqual(["1", "2", "3"]);
    expect(siguienteOrdenUsuarios({ columna: "legajo", direccion: "asc" }, "legajo")).toEqual({
      columna: "legajo",
      direccion: "desc",
    });
    expect(siguienteOrdenUsuarios({ columna: "legajo", direccion: "desc" }, "legajo")).toBeNull();
  });
});
