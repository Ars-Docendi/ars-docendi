import { describe, it, expect } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

import { FranjaDeEstado } from "./components/FranjaDeEstado";
import { respuesta } from "./test/soporte";
import type { TurnoDeLaConversacion } from "./types";

// ============================================================
// La franja de estado: una sola fila con el indicador y las métricas.
//
// Que los dos queden FUERA de la región viva lo fija `asistente.test.tsx`
// montando el panel entero. Acá se fija lo que la franja promete por sí misma:
// que junta a los dos sin cambiarles el contrato.
// ============================================================

const TURNOS: TurnoDeLaConversacion[] = [
  { id: "turno-1", pregunta: "¿cuántos docentes hay?", respuesta: respuesta() },
];

describe("La franja de estado", () => {
  it("junta el indicador y las métricas en una fila, cada uno con su contrato intacto", async () => {
    render(<FranjaDeEstado enVuelo turnos={TURNOS} onDetener={() => {}} umbralMs={0} />);

    const estado = await screen.findByRole("status");
    // El texto del estado es exactamente ése: los puntos que laten son CSS y no
    // entran en lo que el lector anuncia.
    await waitFor(() => expect(estado.textContent).toBe("Consultando…"));

    const metricas = screen.getByText(/consultas al modelo/);
    expect(metricas).toHaveAttribute("aria-hidden", "true");

    expect(estado.parentElement).toBe(metricas.parentElement);
    expect(estado.parentElement).toHaveClass("adoc-asistente-franja");
  });
});
