import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { EditorDeCupoEnFila } from "./EditorDeCupoEnFila";

describe("EditorDeCupoEnFila", () => {
  it("sin ningún valor resoluble (fila de usuario sin override, sin rol conocido) dice «sin dato»", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const onGuardado = vi.fn();
    const user = userEvent.setup();

    render(
      <EditorDeCupoEnFila
        nombre="Marina Díaz"
        cupoConocido={undefined}
        onGuardar={onGuardar}
        onGuardado={onGuardado}
      />,
    );

    expect(screen.getByText("sin dato")).toBeInTheDocument();
    // El lápiz reemplaza el texto «Editar» (fidelidad con el canvas): el
    // nombre accesible sigue viajando en `aria-label`, no en el texto visible.
    const editar = screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" });
    expect(editar).not.toHaveTextContent("Editar");

    await user.click(editar);
    await user.type(screen.getByLabelText("Cupo diario de Marina Díaz (turnos)"), "20");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(onGuardar).toHaveBeenCalledWith(20));
    expect(onGuardado).toHaveBeenCalledWith(20);
    // El valor mostrado lo controla el padre (`cupoConocido` es un prop): acá
    // sólo hace falta ver que la fila volvió a modo vista, con el botón de
    // editar de nuevo disponible.
    expect(
      screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }),
    ).toBeInTheDocument();
  });

  it("mientras se edita, un hint aclara que 0 desactiva el cupo", async () => {
    const user = userEvent.setup();
    render(
      <EditorDeCupoEnFila
        nombre="docente"
        cupoConocido={20}
        origen="rol"
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    expect(screen.getByText("0 = sin tope.")).toBeInTheDocument();
  });

  it("bajar un cupo ya conocido en esta sesión pide confirmación inline", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();

    render(
      <EditorDeCupoEnFila
        nombre="docente"
        cupoConocido={20}
        onGuardar={onGuardar}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    await user.clear(screen.getByLabelText("Cupo diario de docente (turnos)"));
    await user.type(screen.getByLabelText("Cupo diario de docente (turnos)"), "10");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(await screen.findByText(/¿Bajar de 20 a 10\?/)).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(onGuardar).toHaveBeenCalledWith(10));
  });

  it("bajar a 0 (sin tope) también pide confirmación como cualquier baja", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();

    render(
      <EditorDeCupoEnFila
        nombre="docente"
        cupoConocido={20}
        onGuardar={onGuardar}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    await user.clear(screen.getByLabelText("Cupo diario de docente (turnos)"));
    await user.type(screen.getByLabelText("Cupo diario de docente (turnos)"), "0");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(await screen.findByText(/¿Bajar de 20 a 0\?/)).toBeInTheDocument();
  });

  it("cancelar vuelve al valor conocido sin llamar a guardar", async () => {
    const onGuardar = vi.fn();
    const user = userEvent.setup();

    render(
      <EditorDeCupoEnFila
        nombre="docente"
        cupoConocido={15}
        origen="rol"
        onGuardar={onGuardar}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.getByText("15 por día · del rol")).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();
  });

  it("distingue el default del rol de un override de usuario (tarea 12.8), con la copia del canvas", () => {
    const { rerender } = render(
      <EditorDeCupoEnFila
        nombre="secretaria"
        cupoConocido={20}
        origen="rol"
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );
    expect(screen.getByText("20 por día · del rol")).toBeInTheDocument();

    rerender(
      <EditorDeCupoEnFila
        nombre="Lucía Fernández"
        cupoConocido={3}
        origen="override"
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );
    expect(screen.getByText("3 por día · propio")).toBeInTheDocument();
  });

  it("un cupo en 0 con origen conocido dice «sin tope», nunca «0 por día»", () => {
    render(
      <EditorDeCupoEnFila
        nombre="decanato"
        cupoConocido={0}
        origen="rol"
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    expect(screen.getByText("sin tope · del rol")).toBeInTheDocument();
  });
});
