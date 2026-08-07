import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ModalConfirmacionAccion } from "./ModalConfirmacionAccion";
import type { AccionRevision } from "./ModalConfirmacionAccion";
import type { PedidoDesignacion } from "../types";

const PEDIDO: PedidoDesignacion = {
  id: "p1",
  periodoId: "1",
  catedra: "Programación I",
  carrera: "Ingeniería en Informática",
  docente: { dni: "30111222", nombre: "Lucía Fernández", antiguedad: 5 },
  asignaciones: [{ materia: "Programación I", horas: 6 }],
  cargoActual: "Adjunto",
  dedicacionActual: "Categoría 3",
  novedad: "Cambio de cargo o dedicación",
  horasExternas: 0,
  horasInvestigacion: 0,
  adjuntos: [],
  estado: "en_revision_coordinador",
  prioritario: false,
  historial: [],
};

const ETIQUETA_CONFIRMAR: Record<AccionRevision, string> = {
  aceptar: "Aprobar y enviar",
  rechazar: "Rechazar novedad",
  devolver: "Devolver a Borrador",
  priorizar: "Guardar prioridad",
  despriorizar: "Quitar prioridad",
};

function renderModal(
  accion: AccionRevision | null,
  extra?: Partial<React.ComponentProps<typeof ModalConfirmacionAccion>>,
) {
  const onConfirmar = vi.fn();
  const onCerrar = vi.fn();
  render(
    <ModalConfirmacionAccion
      accion={accion}
      pedido={PEDIDO}
      onConfirmar={onConfirmar}
      onCerrar={onCerrar}
      {...extra}
    />,
  );
  return { onConfirmar, onCerrar };
}

describe("ModalConfirmacionAccion", () => {
  it("no renderiza nada cuando la acción es null", () => {
    renderModal(null);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("aceptar permite confirmar con el comentario vacío (opcional)", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("aceptar");
    const dialog = screen.getByRole("dialog");

    const confirmar = within(dialog).getByRole("button", { name: ETIQUETA_CONFIRMAR.aceptar });
    expect(confirmar).toBeEnabled();

    await user.click(confirmar);
    expect(onConfirmar).toHaveBeenCalledWith("");
  });

  it("rechazar bloquea el confirmar mientras el justificativo está vacío [BR-005]", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("rechazar");
    const dialog = screen.getByRole("dialog");

    const confirmar = within(dialog).getByRole("button", { name: ETIQUETA_CONFIRMAR.rechazar });
    expect(confirmar).toBeDisabled();
    expect(within(dialog).getByText(/obligatorio/i)).toBeInTheDocument();
    expect(onConfirmar).not.toHaveBeenCalled();

    await user.type(within(dialog).getByRole("textbox"), "No cumple los requisitos.");
    expect(confirmar).toBeEnabled();
    await user.click(confirmar);
    expect(onConfirmar).toHaveBeenCalledWith("No cumple los requisitos.");
  });

  it("devolver también exige justificativo para confirmar [BR-005]", () => {
    renderModal("devolver");
    const dialog = screen.getByRole("dialog");
    expect(
      within(dialog).getByRole("button", { name: ETIQUETA_CONFIRMAR.devolver }),
    ).toBeDisabled();
  });

  it("priorizar exige justificativo para confirmar [BR-017]", () => {
    renderModal("priorizar");
    const dialog = screen.getByRole("dialog");
    expect(
      within(dialog).getByRole("button", { name: ETIQUETA_CONFIRMAR.priorizar }),
    ).toBeDisabled();
  });

  it("despriorizar permite confirmar con el comentario vacío (sin justificativo)", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("despriorizar");
    const dialog = screen.getByRole("dialog");

    const confirmar = within(dialog).getByRole("button", {
      name: ETIQUETA_CONFIRMAR.despriorizar,
    });
    expect(confirmar).toBeEnabled();

    await user.click(confirmar);
    expect(onConfirmar).toHaveBeenCalledWith("");
  });

  it("pre-carga el comentario del panel y permite editarlo antes de confirmar", async () => {
    const user = userEvent.setup();
    const { onConfirmar } = renderModal("aceptar", { comentarioInicial: "Observación original" });
    const dialog = screen.getByRole("dialog");

    const textarea = within(dialog).getByRole("textbox");
    expect(textarea).toHaveValue("Observación original");

    await user.clear(textarea);
    await user.type(textarea, "Observación editada");
    await user.click(within(dialog).getByRole("button", { name: ETIQUETA_CONFIRMAR.aceptar }));
    expect(onConfirmar).toHaveBeenCalledWith("Observación editada");
  });

  it("Cancelar cierra el modal sin confirmar la acción", async () => {
    const user = userEvent.setup();
    const { onConfirmar, onCerrar } = renderModal("rechazar");
    const dialog = screen.getByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Cancelar" }));
    expect(onCerrar).toHaveBeenCalledTimes(1);
    expect(onConfirmar).not.toHaveBeenCalled();
  });

  it("muestra el título y el subtítulo propios de cada acción", () => {
    renderModal("devolver");
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("Devolver pedido")).toBeInTheDocument();
    expect(
      within(dialog).getByText("Vuelve al Jefe de Cátedra · estado Devuelto"),
    ).toBeInTheDocument();
  });
});
