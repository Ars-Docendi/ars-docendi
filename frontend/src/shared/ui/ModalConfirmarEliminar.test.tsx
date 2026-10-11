import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ModalConfirmarEliminar } from "./ModalConfirmarEliminar";

function renderModal(props: { eliminando?: boolean; error?: string } = {}) {
  const onConfirmar = vi.fn();
  const onOpenChange = vi.fn();
  render(
    <ModalConfirmarEliminar
      open
      onOpenChange={onOpenChange}
      titulo="Eliminar período"
      objeto={
        <>
          el período <strong>"Segundo cuatrimestre 2026"</strong>
        </>
      }
      onConfirmar={onConfirmar}
      {...props}
    />,
  );
  return { onConfirmar, onOpenChange };
}

describe("ModalConfirmarEliminar", () => {
  it("pregunta qué se elimina y avisa que no se puede deshacer", async () => {
    const { onConfirmar } = renderModal();

    expect(screen.getByRole("dialog", { name: "Eliminar período" })).toBeInTheDocument();
    expect(screen.getByText(/¿Estás seguro de que querés eliminar el período/)).toHaveTextContent(
      '¿Estás seguro de que querés eliminar el período "Segundo cuatrimestre 2026"?',
    );
    expect(screen.getByText("Esta acción no se puede deshacer.")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Eliminar" }));
    expect(onConfirmar).toHaveBeenCalledOnce();
  });

  it("mientras elimina no deja cancelar ni confirmar otra vez", () => {
    renderModal({ eliminando: true });

    expect(screen.getByRole("button", { name: "Cancelar" })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Eliminar/ })).toHaveAttribute("aria-busy", "true");
  });

  it("muestra el motivo cuando el borrado falla", () => {
    renderModal({ error: "El período tiene pedidos asociados." });

    expect(screen.getByText("No se pudo eliminar")).toBeInTheDocument();
    expect(screen.getByText("El período tiene pedidos asociados.")).toBeInTheDocument();
  });
});
