import { StrictMode, useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ModalDeFormulario } from "./ModalDeFormulario";

function Escenario({
  pendiente = false,
  conCampo = true,
  sucioInicial = false,
}: {
  pendiente?: boolean;
  conCampo?: boolean;
  sucioInicial?: boolean;
}) {
  const [abierto, setAbierto] = useState(false);
  const [texto, setTexto] = useState(sucioInicial ? "algo" : "");
  return (
    <>
      <h1>Pantalla</h1>
      <button onClick={() => setAbierto(true)}>Abrir</button>
      {abierto && (
        <ModalDeFormulario
          titulo="Título del modal"
          tamano={conCampo ? "formulario" : "confirmacion"}
          onCerrar={() => setAbierto(false)}
          pendiente={pendiente}
          sucio={texto !== ""}
          pie={
            <>
              <button>Volver</button>
              <button className="adoc-btn v-destructive">Borrar</button>
            </>
          }
        >
          {conCampo ? (
            <>
              <input
                aria-label="Primero"
                value={texto}
                onChange={(e) => setTexto(e.target.value)}
              />
              <input aria-label="Segundo" />
            </>
          ) : (
            <p>¿Seguro?</p>
          )}
        </ModalDeFormulario>
      )}
    </>
  );
}

describe("ModalDeFormulario", () => {
  it("is a labelled modal dialog and focuses the first field, returning focus to the opener", async () => {
    const usuario = userEvent.setup();
    render(<Escenario />);
    const abridor = screen.getByRole("button", { name: "Abrir" });
    await usuario.click(abridor);

    const dialogo = screen.getByRole("dialog", { name: "Título del modal" });
    expect(dialogo).toHaveAttribute("aria-modal", "true");
    expect(screen.getByLabelText("Primero")).toHaveFocus();

    await usuario.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await vi.waitFor(() => expect(abridor).toHaveFocus());
  });

  it("keeps the focus inside the dialog under StrictMode's double effect run", async () => {
    const usuario = userEvent.setup();
    render(
      <StrictMode>
        <Escenario />
      </StrictMode>,
    );
    await usuario.click(screen.getByRole("button", { name: "Abrir" }));
    await new Promise((resolver) => window.setTimeout(resolver, 20));

    expect(screen.getByLabelText("Primero")).toHaveFocus();
  });

  it("without fields, focuses the least destructive button", async () => {
    const usuario = userEvent.setup();
    render(<Escenario conCampo={false} />);
    await usuario.click(screen.getByRole("button", { name: "Abrir" }));

    expect(screen.getByRole("button", { name: "Volver" })).toHaveFocus();
  });

  it("keeps Tab inside the dialog in both directions", async () => {
    const usuario = userEvent.setup();
    render(<Escenario />);
    await usuario.click(screen.getByRole("button", { name: "Abrir" }));

    await usuario.tab({ shift: true });
    expect(screen.getByRole("button", { name: "Cerrar" })).toHaveFocus();
    // From the first element backwards it wraps to the last, and forwards from the last to the first.
    await usuario.tab({ shift: true });
    expect(screen.getByRole("button", { name: "Borrar" })).toHaveFocus();
    await usuario.tab();
    expect(screen.getByRole("button", { name: "Cerrar" })).toHaveFocus();
  });

  it("asks for confirmation inline when closing with unsaved changes", async () => {
    const usuario = userEvent.setup();
    render(<Escenario />);
    await usuario.click(screen.getByRole("button", { name: "Abrir" }));
    await usuario.type(screen.getByLabelText("Primero"), "x");

    await usuario.keyboard("{Escape}");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Seguir editando" })).toHaveFocus();

    // Esc over the confirmation means "keep editing".
    await usuario.keyboard("{Escape}");
    expect(screen.queryByText("Tenés cambios sin guardar.")).not.toBeInTheDocument();
    await vi.waitFor(() => expect(screen.getByLabelText("Primero")).toHaveFocus());

    await usuario.click(screen.getByRole("button", { name: "Cerrar" }));
    await usuario.click(screen.getByRole("button", { name: "Descartar cambios" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("does not dismiss while a mutation is pending", async () => {
    const usuario = userEvent.setup();
    render(<Escenario pendiente />);
    await usuario.click(screen.getByRole("button", { name: "Abrir" }));

    await usuario.keyboard("{Escape}");
    expect(screen.getByRole("button", { name: "Cerrar" })).toBeDisabled();
    const fondo = document.querySelector(".adoc-modal-stage") as HTMLElement;
    fireEvent.mouseDown(fondo);
    fireEvent.mouseUp(fondo);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("closes from the overlay only when the press and the release both land on it", async () => {
    const usuario = userEvent.setup();
    render(<Escenario />);
    await usuario.click(screen.getByRole("button", { name: "Abrir" }));
    const fondo = document.querySelector(".adoc-modal-stage") as HTMLElement;
    const dialogo = screen.getByRole("dialog");

    fireEvent.mouseDown(dialogo);
    fireEvent.mouseUp(fondo);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    fireEvent.mouseDown(fondo);
    fireEvent.mouseUp(dialogo);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    fireEvent.mouseDown(fondo);
    fireEvent.mouseUp(fondo);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("with stacked modals, Esc only closes the topmost one", async () => {
    const usuario = userEvent.setup();
    function Apilados() {
      const [arriba, setArriba] = useState(false);
      return (
        <>
          <ModalDeFormulario titulo="Abajo" onCerrar={() => {}}>
            <button onClick={() => setArriba(true)}>Abrir segundo</button>
          </ModalDeFormulario>
          {arriba && (
            <ModalDeFormulario titulo="Arriba" onCerrar={() => setArriba(false)}>
              <p>contenido</p>
            </ModalDeFormulario>
          )}
        </>
      );
    }
    render(<Apilados />);
    await usuario.click(screen.getByRole("button", { name: "Abrir segundo" }));
    expect(screen.getAllByRole("dialog")).toHaveLength(2);

    await usuario.keyboard("{Escape}");
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    expect(screen.getByRole("dialog", { name: "Abajo" })).toBeInTheDocument();
  });

  it("returns focus to the page title when the opener is gone", async () => {
    const usuario = userEvent.setup();
    function SinAbridor() {
      const [fase, setFase] = useState<"cerrado" | "abierto">("cerrado");
      return (
        <>
          <h1>Pantalla</h1>
          {fase === "cerrado" && <button onClick={() => setFase("abierto")}>Abrir</button>}
          {fase === "abierto" && (
            <ModalDeFormulario
              titulo="M"
              onCerrar={() => setFase("cerrado")}
              pie={<button>Ok</button>}
            >
              <p>texto</p>
            </ModalDeFormulario>
          )}
        </>
      );
    }
    render(<SinAbridor />);
    const abridor = screen.getByRole("button", { name: "Abrir" });
    await usuario.click(abridor);
    // The opener leaves the DOM while the modal is open.
    abridor.remove();
    await usuario.keyboard("{Escape}");
    await vi.waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Pantalla" })).toHaveFocus(),
    );
  });
});
