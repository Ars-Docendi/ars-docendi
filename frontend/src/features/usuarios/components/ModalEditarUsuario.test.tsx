import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { CatalogosUsuarios } from "../api/usuariosApi";
import type { UsuarioMock } from "../models";
import { ModalEditarUsuario } from "./ModalEditarRol";

const USUARIO: UsuarioMock = {
  id: "usuario-1",
  nombre: "Carla",
  apellido: "López",
  documento: "28341567",
  legajo: "0421",
  cuil: "27-28341567-3",
  fecha_nacimiento: "1980-03-14",
  telefono: "11-4000-0001",
  upn: "carla.lopez@unlam.edu.ar",
  is_active: true,
  roles: ["Docente"],
  membresias: [],
  persona_id: "persona-1",
  perfilDocente: { esDocente: true, cantidadMaterias: 1 },
};

const CATALOGOS: CatalogosUsuarios = {
  roles: [],
  materias: [],
  carreras: [],
};

describe("ModalEditarUsuario", () => {
  it("usa la fecha nativa sin un icono de calendario SVG adicional", () => {
    render(
      <ModalEditarUsuario
        usuario={USUARIO}
        upnsExistentes={[]}
        onGuardar={vi.fn()}
        onCerrar={vi.fn()}
        catalogos={CATALOGOS}
      />,
    );

    const fecha = document.querySelector<HTMLInputElement>('input[type="date"]');

    expect(fecha).toHaveValue("1980-03-14");
    expect(fecha).toHaveAttribute("type", "date");
    expect(document.querySelector(".cal-ico")).not.toBeInTheDocument();
  });
});
