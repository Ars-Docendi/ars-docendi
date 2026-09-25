import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
      materia: { id: "materia-1", codigo: "03500", nombre: "Ingeniería de Software" },
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

  it("el click en la fila abre Editar; Desactivar no lo dispara", async () => {
    const user = userEvent.setup();
    const onEditar = vi.fn();
    const onDesactivar = vi.fn();
    render(
      <TablaDocentes
        docentes={[DOCENTE]}
        onDesactivar={onDesactivar}
        onActivar={vi.fn()}
        onEditar={onEditar}
      />,
    );

    await user.click(screen.getByText("28341567"));
    expect(onEditar).toHaveBeenCalledWith(DOCENTE);

    await user.click(screen.getByRole("button", { name: "Desactivar" }));
    expect(onDesactivar).toHaveBeenCalledOnce();
    expect(onEditar).toHaveBeenCalledOnce();
  });

  it("en solo lectura la fila no es clickeable", async () => {
    const user = userEvent.setup();
    const onEditar = vi.fn();
    render(
      <TablaDocentes
        docentes={[DOCENTE]}
        onDesactivar={vi.fn()}
        onActivar={vi.fn()}
        onEditar={onEditar}
        soloLectura
      />,
    );

    await user.click(screen.getByText("28341567"));
    expect(onEditar).not.toHaveBeenCalled();
    expect(screen.getByText("28341567").closest("tr")).not.toHaveClass("adoc-fila-clickeable");
  });

  it("titula Nombre y recorta el nombre si no entra", () => {
    render(
      <TablaDocentes
        docentes={[DOCENTE]}
        onDesactivar={vi.fn()}
        onActivar={vi.fn()}
        onEditar={vi.fn()}
      />,
    );

    expect(screen.getByRole("columnheader", { name: "Nombre" })).toBeInTheDocument();
    expect(screen.getByText("López, Carla")).toHaveClass("adoc-texto-recortado");
  });
});
