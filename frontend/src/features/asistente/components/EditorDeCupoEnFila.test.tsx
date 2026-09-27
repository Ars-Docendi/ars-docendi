import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { EditorDeCupoEnFila } from "./EditorDeCupoEnFila";

describe("EditorDeCupoEnFila", () => {
  it("el primer guardado de la fila no pide confirmación", async () => {
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

    expect(screen.getByText("sin override propio")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }));
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
        onGuardar={onGuardar}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.getByText("15")).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();
  });

  it("distingue el default del rol de un override de usuario (tarea 12.8)", () => {
    const { rerender } = render(
      <EditorDeCupoEnFila
        nombre="secretaria"
        cupoConocido={20}
        origen="rol"
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );
    expect(screen.getByText("(del rol)")).toBeInTheDocument();

    rerender(
      <EditorDeCupoEnFila
        nombre="Lucía Fernández"
        cupoConocido={3}
        origen="override"
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );
    expect(screen.getByText("(override)")).toBeInTheDocument();
  });
});
