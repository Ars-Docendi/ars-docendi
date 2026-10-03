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
  codigosDeRol: [],
  cupoEfectivo: null,
  origenDeCupo: null,
  accesoEfectivo: null,
  origenDeAcceso: null,
};

/** Busca el texto agregado de un contenedor cuyos hijos lo partieron en
 * varios nodos (prefijo «US$» + número), como hace `ValorConPrefijo`. */
function textoCompuesto(texto: string) {
  return (_content: string, elemento: Element | null) => elemento?.textContent === texto;
}

describe("KpisDeUso", () => {
  it("no renderiza nada mientras el uso todavía no llegó", () => {
    const { container } = render(
      <KpisDeUso organizacion={undefined} usuariosActivos={undefined} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("muestra sesiones, llamadas, costo estimado (con prefijo US$ aparte) y latencia p95 organizacionales", () => {
    render(<KpisDeUso organizacion={ORGANIZACION} usuariosActivos={5} />);

    expect(screen.getByText("Sesiones")).toBeInTheDocument();
    expect(screen.getByText("40")).toBeInTheDocument();
    expect(screen.getByText("5 usuarios activos")).toBeInTheDocument();

    expect(screen.getByText("Llamadas")).toBeInTheDocument();
    expect(screen.getByText("70")).toBeInTheDocument();
    expect(screen.getByText("1,8 por sesión")).toBeInTheDocument();

    expect(screen.getByText("Costo estimado")).toBeInTheDocument();
    expect(screen.getByText(textoCompuesto("US$5,50"))).toBeInTheDocument();
    expect(screen.getByText("Estimado; manda la factura.")).toBeInTheDocument();

    expect(screen.getByText("Latencia p95")).toBeInTheDocument();
    expect(screen.getByText(/Promedio\s*0,9\s*s/)).toBeInTheDocument();
  });

  it("un usuario activo (singular) no dice «1 usuarios activos»", () => {
    render(<KpisDeUso organizacion={ORGANIZACION} usuariosActivos={1} />);

    expect(screen.getByText("1 usuario activo")).toBeInTheDocument();
  });

  it("un turno sin precio vigente se ve aparte, nunca como costo cero silencioso", () => {
    render(
      <KpisDeUso organizacion={{ ...ORGANIZACION, turnosSinPrecio: 3 }} usuariosActivos={5} />,
    );

    expect(screen.getByText("3 turnos sin precio")).toBeInTheDocument();
    expect(screen.queryByText("Estimado; manda la factura.")).not.toBeInTheDocument();
  });
});
