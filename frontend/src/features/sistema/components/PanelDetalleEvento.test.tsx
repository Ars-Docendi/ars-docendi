import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDetalleEvento } from "./PanelDetalleEvento";
import type { EventoAuditoria } from "../api/auditoriaApi";

function evento(sobrescribir: Partial<EventoAuditoria>): EventoAuditoria {
  return {
    id: "cambios-1",
    origen: "cambios",
    schema: "identity",
    tabla: "personas",
    rowPk: "1042",
    accion: "UPDATE",
    cambiadoEn: "2026-09-26T22:10:04.000Z",
    cambiadoPor: null,
    requestId: "req-abc",
    columnasCambiadas: ["estado", "documento"],
    cambios: [
      {
        campo: "estado",
        etiquetaCampo: "Estado",
        valorAnterior: "pendiente",
        valorNuevo: "aprobado",
        oculto: false,
      },
      {
        campo: "documento",
        etiquetaCampo: "Documento",
        valorAnterior: null,
        valorNuevo: null,
        oculto: true,
      },
    ],
    actor: "Lucía Fernández",
    tipoActor: "persona",
    accionEtiqueta: "Cambio",
    modulo: "Identidad",
    objeto: "Solicitud #1042",
    resumen: "Solicitud #1042: Estado pendiente → aprobado",
    ...sobrescribir,
  };
}

describe("PanelDetalleEvento", () => {
  it("muestra el campo enmascarado con candado y el campo seguro con antes/después", () => {
    render(<PanelDetalleEvento evento={evento({})} onCerrar={vi.fn()} />);

    expect(screen.getByText("Enmascarado por política")).toBeInTheDocument();
    expect(screen.getByText("pendiente")).toBeInTheDocument();
    expect(screen.getByText("aprobado")).toBeInTheDocument();
    expect(screen.queryByText(/30111222/)).not.toBeInTheDocument();
  });

  it("«Cerrar detalle» llama al callback", async () => {
    const user = userEvent.setup();
    const onCerrar = vi.fn();
    render(<PanelDetalleEvento evento={evento({})} onCerrar={onCerrar} />);

    await user.click(screen.getByRole("button", { name: "Cerrar detalle" }));

    expect(onCerrar).toHaveBeenCalled();
  });

  it("al abrir, el foco va al título del panel", async () => {
    render(<PanelDetalleEvento evento={evento({})} onCerrar={vi.fn()} />);

    expect(document.activeElement).toHaveTextContent(
      "Solicitud #1042: Estado pendiente → aprobado",
    );
  });
});
