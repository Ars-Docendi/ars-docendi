import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import { KpisDeUso } from "./KpisDeUso";
import type { UsoAgregado } from "../types";

const ORGANIZACION: UsoAgregado = {
  clave: "organizacion",
  turnos: 40,
  porEstado: { respondida: 38, servicio_degradado: 2 },
  llamadasAlModelo: 70,
  tokensDeEntrada: 8000,
  tokensDeSalida: 2000,
  tokensDeCache: 500,
  latenciaPromedioMs: 900,
  latenciaP95Ms: 1600,
  proveedores: ["anthropic"],
  costoEstimado: 5.5,
  esEstimado: true,
  turnosSinPrecio: 0,
};

describe("KpisDeUso", () => {
  it("no renderiza nada mientras el uso todavía no llegó", () => {
    const { container } = render(<KpisDeUso organizacion={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("muestra sesiones, llamadas, costo estimado y latencia p95 organizacionales", () => {
    render(<KpisDeUso organizacion={ORGANIZACION} />);

    expect(screen.getByText("Sesiones")).toBeInTheDocument();
    expect(screen.getByText("40")).toBeInTheDocument();
    expect(screen.getByText("Llamadas al modelo")).toBeInTheDocument();
    expect(screen.getByText("70")).toBeInTheDocument();
    expect(screen.getByText(/US\$\s*5,50 \(estimado\)/)).toBeInTheDocument();
    expect(screen.getByText("Latencia p95")).toBeInTheDocument();
  });

  it("un turno sin precio vigente se ve aparte, nunca como costo cero silencioso", () => {
    render(<KpisDeUso organizacion={{ ...ORGANIZACION, turnosSinPrecio: 3 }} />);

    expect(screen.getByText("3 turnos sin precio")).toBeInTheDocument();
  });
});
