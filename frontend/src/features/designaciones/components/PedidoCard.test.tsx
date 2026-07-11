import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { PedidoCard } from "./PedidoCard";
import type {
  AccionHistorial,
  ActorContexto,
  EstadoPedido,
  EventoHistorial,
  PedidoDesignacion,
} from "../types";

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};

function evento(accion: AccionHistorial, comentario?: string): EventoHistorial {
  return {
    id: "e1",
    accion,
    porRol: "Coordinador",
    porNombre: "M. Díaz",
    etapa: "en_revision_coordinador",
    comentario,
    fecha: "2026-01-01T00:00:00.000Z",
  };
}

function pedido(
  estado: EstadoPedido,
  overrides: Partial<PedidoDesignacion> = {},
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
    historial: [],
    ...overrides,
  };
}

describe("PedidoCard — presentación de rechazados", () => {
  it("muestra el chip 'Rechazado' (no el de novedad) y el motivo como cita destacada", () => {
    const rechazado = pedido("rechazado", {
      novedad: "Cambio de cargo o dedicación",
      historial: [evento("rechazar", "Falta resolución del Consejo Departamental")],
    });

    render(<PedidoCard pedido={rechazado} actor={COORD} onSeleccionar={vi.fn()} />);

    expect(screen.getByText("Rechazado")).toBeInTheDocument();
    // El chip de novedad ("Cambio") NO aparece en una card rechazada.
    expect(screen.queryByText("Cambio")).not.toBeInTheDocument();
    // El motivo se cita textual (sin el prefijo "Rechazado:").
    expect(screen.getByText(/Falta resolución del Consejo Departamental/)).toBeInTheDocument();
    expect(screen.queryByText(/Rechazado:/)).not.toBeInTheDocument();
  });

  it("un pedido devuelto conserva el chip de novedad y el detalle plano 'Devuelto: …'", () => {
    const devuelto = pedido("devuelto", {
      novedad: "Cambio de cargo o dedicación",
      historial: [evento("devolver", "faltan datos")],
    });

    render(<PedidoCard pedido={devuelto} actor={COORD} onSeleccionar={vi.fn()} />);

    expect(screen.getByText("Cambio")).toBeInTheDocument();
    expect(screen.getByText("Devuelto: faltan datos")).toBeInTheDocument();
    expect(screen.queryByText("Rechazado")).not.toBeInTheDocument();
  });
});
