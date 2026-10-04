import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";

import { ServidorDelModeloCard } from "./ServidorDelModeloCard";
import type { ServidorLocal } from "../types";

// La tarjeta de carga del servidor del modelo propio
// (asistente-optimizaciones-modelo-local, D8).

const BASE: ServidorLocal = {
  configurado: true,
  alcanzable: true,
  motor: "vllm",
  enCurso: 3,
  enEspera: 0,
  usoDeKvCache: 0.42,
  aciertosDeCacheDePrefijo: 0.875,
  compuerta: { capacidad: 4, enCurso: 2, enEspera: 1 },
};

function valorDe(termino: string): string | null {
  return screen.getByText(termino).nextElementSibling?.textContent ?? null;
}

describe("ServidorDelModeloCard", () => {
  it("no aparece si el proveedor no es local", () => {
    const { container } = render(
      <ServidorDelModeloCard servidor={{ ...BASE, configurado: false }} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("no aparece mientras la consulta no contestó", () => {
    const { container } = render(<ServidorDelModeloCard servidor={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("muestra la carga, la caché y la compuerta", () => {
    render(<ServidorDelModeloCard servidor={BASE} />);

    expect(screen.getByRole("region", { name: "Servidor del modelo" })).toHaveTextContent(
      "Servidor del modelo · vllm",
    );
    expect(valorDe("En curso")).toBe("3");
    expect(valorDe("En espera")).toBe("0");
    expect(valorDe("KV cache")).toBe("42 %");
    expect(valorDe("Caché de prefijo")).toBe("88 %");
    expect(screen.getByText(/2 de 4 en curso, 1 en cola/)).toBeInTheDocument();
  });

  it("una métrica que el servidor no publica es «—», no cero", () => {
    render(
      <ServidorDelModeloCard
        servidor={{ ...BASE, motor: "llama.cpp", aciertosDeCacheDePrefijo: null, compuerta: null }}
      />,
    );

    expect(valorDe("Caché de prefijo")).toBe("—");
    expect(screen.queryByText(/Compuerta del backend/)).not.toBeInTheDocument();
  });

  it("avisa cuando el servidor no responde", () => {
    render(<ServidorDelModeloCard servidor={{ ...BASE, alcanzable: false }} />);

    expect(screen.getByText(/No responde sus métricas/)).toBeInTheDocument();
    expect(screen.queryByText("En curso")).not.toBeInTheDocument();
  });
});
