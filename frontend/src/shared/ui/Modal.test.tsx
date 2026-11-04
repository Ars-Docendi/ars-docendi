import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";

import { Modal } from "./Modal";

function Apilados({ pendiente = false, sucio = false }: { pendiente?: boolean; sucio?: boolean }) {
  const [formulario, setFormulario] = useState(false);
  const [confirmacion, setConfirmacion] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setFormulario(true)}>
        Abrir formulario
      </button>
      <Modal
        open={formulario}
        onOpenChange={setFormulario}
        title="Formulario"
        sucio={sucio}
        pendiente={pendiente}
        footer={
          <button type="button" onClick={() => setConfirmacion(true)}>
            Eliminar
          </button>
        }
      >
        <input aria-label="Nombre" />
      </Modal>
      <Modal
        open={confirmacion}
        onOpenChange={setConfirmacion}
        title="Confirmar"
        tamano="confirmacion"
        footer={<button type="button">Aceptar</button>}
      >
        <p>¿Seguro?</p>
      </Modal>
    </>
  );
}

describe("Modal de la app", () => {
  it("Esc cierra sólo el diálogo superior y el foco vuelve a quien lo abrió", async () => {
    render(<Apilados />);
    const abridor = screen.getByRole("button", { name: "Abrir formulario" });
    await userEvent.click(abridor);
    expect(screen.getByLabelText("Nombre")).toHaveFocus();

    await userEvent.click(screen.getByRole("button", { name: "Eliminar" }));
    expect(screen.getByRole("dialog", { name: "Confirmar" })).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog", { name: "Confirmar" })).not.toBeInTheDocument();
    expect(screen.getByRole("dialog", { name: "Formulario" })).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await Promise.resolve();
    await new Promise((resolver) => setTimeout(resolver, 10));
    expect(abridor).toHaveFocus();
  });

  it("con datos sin guardar, Esc pide confirmar y 'Seguir editando' conserva lo escrito", async () => {
    render(<Apilados sucio />);
    await userEvent.click(screen.getByRole("button", { name: "Abrir formulario" }));
    await userEvent.type(screen.getByLabelText("Nombre"), "Ana");

    await userEvent.keyboard("{Escape}");
    expect(screen.getByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Seguir editando" }));
    expect(screen.getByLabelText("Nombre")).toHaveValue("Ana");

    await userEvent.keyboard("{Escape}");
    await userEvent.click(screen.getByRole("button", { name: "Descartar cambios" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("no se cierra con Esc ni con la X mientras hay una mutación pendiente", async () => {
    render(<Apilados pendiente />);
    await userEvent.click(screen.getByRole("button", { name: "Abrir formulario" }));

    await userEvent.keyboard("{Escape}");
    expect(screen.getByRole("dialog", { name: "Formulario" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cerrar" })).toBeDisabled();
  });
});
