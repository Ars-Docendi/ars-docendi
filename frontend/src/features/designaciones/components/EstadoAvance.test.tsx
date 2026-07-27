import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { EstadoAvance } from "./EstadoAvance";
import type { EstadoPedido, EventoHistorial, PedidoDesignacion } from "../types";

function pedido(
  estado: EstadoPedido,
  historial: EventoHistorial[] = [],
  etapaRetorno?: EstadoPedido,
): PedidoDesignacion {
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
    historial,
    etapaRetorno,
  };
}

describe("EstadoAvance (celda Estado combinada)", () => {
  it("en revisión: muestra la etapa con su avance x/4", () => {
    render(<EstadoAvance pedido={pedido("en_revision_secretaria")} />);
    expect(screen.getByText("En Secretaría · 2/4")).toBeInTheDocument();
  });

  it("aceptado (en lote): muestra un punto verde y 'Aceptado', sin el stepper de 4 barras", () => {
    const { container } = render(<EstadoAvance pedido={pedido("en_lote")} />);
    expect(screen.getByText("Aceptado")).toBeInTheDocument();
    expect(container.querySelector(".adoc-estado-dot")).toBeInTheDocument();
    expect(container.querySelector(".adoc-pedido-stepper")).not.toBeInTheDocument();
  });

  it("devuelto: mantiene el mismo stepper + 'En {etapa} · x/4' que un estado en revisión, con 'Devuelto por {revisor} ({rol})' al costado", () => {
    const evento: EventoHistorial = {
      id: "e1",
      accion: "devolver",
      porRol: "Secretaría",
      porNombre: "S. Gómez",
      etapa: "en_revision_decanato",
      comentario: "falta adjunto",
      fecha: "2026-01-01T00:00:00.000Z",
    };
    const { container } = render(
      <EstadoAvance pedido={pedido("devuelto", [evento], "en_revision_decanato")} />,
    );
    expect(
      screen.getByText("En Decanato · 3/4 · Devuelto por S. Gómez (Secretaría)"),
    ).toBeInTheDocument();
    // El stepper (mismo componente que un estado en revisión) sigue ahí — no un dot.
    expect(container.querySelector(".adoc-pedido-stepper")).toBeInTheDocument();
    expect(container.querySelector(".adoc-estado-dot")).not.toBeInTheDocument();
  });

  it("devuelto sin evento de devolución en el historial: usa '—' como revisor, conserva la etapa y el stepper", () => {
    render(<EstadoAvance pedido={pedido("devuelto", [], "en_revision_coordinador")} />);
    expect(screen.getByText("En Coordinación · 1/4 · Devuelto por —")).toBeInTheDocument();
  });

  it("devuelto sin etapaRetorno (no debería pasar — invariante de dominio): sin stepper, solo 'Devuelto por {revisor}'", () => {
    const { container } = render(<EstadoAvance pedido={pedido("devuelto")} />);
    expect(screen.getByText("Devuelto por —")).toBeInTheDocument();
    expect(container.querySelector(".adoc-pedido-stepper")).not.toBeInTheDocument();
    expect(container.querySelector(".adoc-estado-dot")).toBeInTheDocument();
  });

  it("rechazado: muestra 'Rechazado' sin avance x/4", () => {
    render(<EstadoAvance pedido={pedido("rechazado")} />);
    expect(screen.getByText("Rechazado")).toBeInTheDocument();
    expect(screen.queryByText(/\/4/)).not.toBeInTheDocument();
  });
});
