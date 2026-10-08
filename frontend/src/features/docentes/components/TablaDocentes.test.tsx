import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { DocenteMock } from "../models";
import { TablaDocentes } from "./TablaDocentes";

const DOCENTE: DocenteMock = {
  id: "persona-1",
  nombre: "Carla",
  apellido: "López",
  documento: "28341567",
  legajo: "0421",
  cuil: "27-28341567-3",
  fecha_nacimiento: "1980-03-14",
  telefono: "11-4000-0001",
  upn: "carla.lopez@unlam.edu.ar",
  roles: ["Docente"],
  membresias: [],
  tieneCuenta: true,
  asignaciones: [
    {
      materiaId: "materia-1",
      materiaCodigo: "03500",
      materiaNombre: "Ingeniería de Software",
      carreraId: "carrera-1",
      cargo: "Profesor Adjunto",
      cargoAbreviatura: "Adjunto",
      horas: 10,
    },
  ],
  is_active: true,
};

describe("TablaDocentes", () => {
  it("oculta todas las acciones cuando la vista es de solo lectura", () => {
    render(
      <TablaDocentes
        docentes={[DOCENTE]}
        onDesactivar={vi.fn()}
        onActivar={vi.fn()}
        onEditar={vi.fn()}
        soloLectura
      />,
    );

    expect(screen.queryByRole("columnheader", { name: "Acciones" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Editar" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Desactivar" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Activar" })).not.toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /^Filtrar / })).not.toHaveLength(0);
  });
});
