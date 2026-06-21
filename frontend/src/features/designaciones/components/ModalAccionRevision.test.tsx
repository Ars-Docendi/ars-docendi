import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ModalAccionRevision } from "./ModalAccionRevision";
import type { PedidoDesignacion } from "../types";

const PEDIDO: PedidoDesignacion = {
  id: "p1",
  periodoId: "1",
  catedra: "Ingeniería de Software",
  carrera: "Ingeniería en Informática",
  docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
  materiaAsociada: "Ingeniería de Software",
  cargoActual: "Adjunto",
  dedicacionActual: "Categoría 3",
  novedad: "Sin novedad",
  haceHorasOtroDepto: false,
  adjuntos: [],
  estado: "en_revision_coordinador",
  prioritario: false,
  historial: [],
};

function renderModal(accion: "aceptar" | "rechazar" | "devolver" | "priorizar") {
  const onConfirmar = vi.fn();
  const onCerrar = vi.fn();
  render(
    <ModalAccionRevision
      accion={accion}
      pedido={PEDIDO}
      onConfirmar={onConfirmar}
      onCerrar={onCerrar}
    />,
  );
  return { onConfirmar, onCerrar };
}

describe("ModalAccionRevision", () => {
  it("rechazar exige comentario: el confirm vacío se bloquea [BR-005]", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("rechazar");
    const dialog = screen.getByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Rechazar" }));

    expect(onConfirmar).not.toHaveBeenCalled();
    expect(within(dialog).getByRole("alert")).toBeInTheDocument();
  });

  it("rechazar con comentario dispara onConfirmar con el texto", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("rechazar");
    const dialog = screen.getByRole("dialog");

    await user.type(within(dialog).getByRole("textbox"), "No cumple los requisitos");
    await user.click(within(dialog).getByRole("button", { name: "Rechazar" }));

    expect(onConfirmar).toHaveBeenCalledWith("No cumple los requisitos");
  });

  it("devolver también exige comentario [BR-005]", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("devolver");
    const dialog = screen.getByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Devolver" }));

    expect(onConfirmar).not.toHaveBeenCalled();
  });

  it("aceptar no exige comentario: confirma con string vacío", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("aceptar");
    const dialog = screen.getByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Aceptar" }));

    expect(onConfirmar).toHaveBeenCalledWith("");
  });
});
