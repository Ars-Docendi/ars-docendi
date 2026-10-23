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

  it("un evento del asistente sin requestId muestra «Solicitud —»", () => {
    render(
      <PanelDetalleEvento
        evento={evento({
          requestId: null,
          tabla: "modo_mantenimiento",
          schema: "asistente",
          rowPk: "—",
        })}
        onCerrar={vi.fn()}
      />,
    );

    const dd = screen.getByText("Tabla").nextElementSibling;
    expect(dd).toHaveTextContent("asistente.modo_mantenimiento");
    expect(screen.getByText("Solicitud").nextElementSibling).toHaveTextContent("—");
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

  it("el chip de acción vive dentro del encabezado que le da align-self:flex-start", () => {
    // jsdom no calcula layout: no se puede medir el ancho del chip acá. Lo que
    // sí se puede probar es que el selector CSS
    // ".auditoria-detalle-encabezado .sistema-chip-accion" (sistema.css)
    // efectivamente matchea esta estructura — si alguien mueve el chip fuera
    // del encabezado, este test lo detecta.
    render(<PanelDetalleEvento evento={evento({})} onCerrar={vi.fn()} />);

    const chip = screen.getByText("Cambio");
    expect(chip).toHaveClass("sistema-chip-accion");
    expect(chip.closest(".auditoria-detalle-encabezado")).not.toBeNull();
  });

  it("un valor largo conserva la clase que activa el ajuste de línea (no desborda)", () => {
    const valorLargo = "2026-09-27T01:52:30.81236+00:00-y-un-resto-bien-largo-que-podria-desbordar";
    render(
      <PanelDetalleEvento
        evento={evento({
          cambios: [
            {
              campo: "created_at",
              etiquetaCampo: "Fecha de creación",
              valorAnterior: null,
              valorNuevo: valorLargo,
              oculto: false,
            },
          ],
        })}
        onCerrar={vi.fn()}
      />,
    );

    expect(screen.getByText(valorLargo)).toHaveClass("auditoria-valor-despues");
  });
});
