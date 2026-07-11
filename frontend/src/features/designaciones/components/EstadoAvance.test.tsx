import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { EstadoAvance } from "./EstadoAvance";
import type { EstadoPedido, PedidoDesignacion } from "../types";

function pedido(estado: EstadoPedido): PedidoDesignacion {
  return {
    id: "p1",
    periodoId: "1",
    catedra: "Cátedra X",
    carrera: "Ingeniería en Informática",
    docente: { dni: "1", nombre: "Docente Uno", antiguedad: 3 },
    asignaciones: [{ materia: "Materia X", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    horasExternas: 0,
    horasInvestigacion: 0,
    adjuntos: [],
    estado,
    prioritario: false,
    historial: [],
  };
}

describe("EstadoAvance (celda Estado combinada)", () => {
  it("en revisión: muestra la etapa con su avance x/4", () => {
    render(<EstadoAvance pedido={pedido("en_revision_secretaria")} />);
    expect(screen.getByText("En Secretaría · 2/4")).toBeInTheDocument();
  });

  it("aceptado (en lote): muestra 'Aceptado'", () => {
    render(<EstadoAvance pedido={pedido("en_lote")} />);
    expect(screen.getByText("Aceptado")).toBeInTheDocument();
  });

  it("devuelto y rechazado: muestran su estado sin avance x/4", () => {
    const { rerender } = render(<EstadoAvance pedido={pedido("devuelto")} />);
    expect(screen.getByText("Devuelto")).toBeInTheDocument();
    expect(screen.queryByText(/\/4/)).not.toBeInTheDocument();

    rerender(<EstadoAvance pedido={pedido("rechazado")} />);
    expect(screen.getByText("Rechazado")).toBeInTheDocument();
  });
});
