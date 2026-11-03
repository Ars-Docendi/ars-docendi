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
    // El valor mostrado lo controla el padre (`cupoConocido` es un prop): acá sólo hace falta ver
    // que la fila volvió a modo vista, con el botón de editar de nuevo disponible.
    expect(
      screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }),
    ).toBeInTheDocument();
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

  it("si el guardado falla, lo anuncia y deja la edición abierta para reintentar", async () => {
    const onGuardado = vi.fn();
    const user = userEvent.setup();

    render(
      <EditorDeCupoEnFila
        nombre="docente"
        cupoConocido={15}
        origen="rol"
        onGuardar={vi.fn().mockRejectedValue(new Error("500"))}
        onGuardado={onGuardado}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    await user.clear(screen.getByLabelText("Cupo diario de docente (turnos)"));
    await user.type(screen.getByLabelText("Cupo diario de docente (turnos)"), "20");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "No se pudo guardar el cupo de docente.",
    );
    expect(screen.getByLabelText("Cupo diario de docente (turnos)")).toHaveValue(20);
    expect(onGuardado).not.toHaveBeenCalled();
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
