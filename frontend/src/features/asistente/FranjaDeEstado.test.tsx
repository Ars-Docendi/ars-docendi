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
// que junta a los dos sin cambiarles el contrato. «Dejar de esperar» ya no
// vive acá (asistente-rediseno-v3, D14): se mudó al composer, y lo prueba
// `EntradaDePregunta.test.tsx`.
// ============================================================

const TURNOS: TurnoDeLaConversacion[] = [
  { id: "turno-1", pregunta: "¿cuántos docentes hay?", respuesta: respuesta() },
];

describe("La franja de estado", () => {
  it("junta el indicador y las métricas en una fila, cada uno con su contrato intacto (modo debug)", async () => {
    render(<FranjaDeEstado enVuelo turnos={TURNOS} umbralMs={0} debug />);

    const estado = await screen.findByRole("status");
    // El texto del estado es exactamente ése. Es sólo accesible —`sr-only`—:
    // los puntos que laten y el texto visibles se mudaron al turno en vuelo.
    await waitFor(() => expect(estado.textContent).toBe("Consultando…"));
    expect(estado).toHaveClass("adoc-sr");

    const metricas = screen.getByText(/consultas al modelo/);
    expect(metricas).toHaveAttribute("aria-hidden", "true");

    expect(estado.parentElement).toBe(metricas.parentElement);
    expect(estado.parentElement).toHaveClass("adoc-asistente-franja");
  });

  // Decisión 14 del PO (2026-09-26): la línea de métricas sólo se muestra en
  // modo debug (`VITE_ASISTENTE_DEBUG`), el mismo switch que gatea «Cómo lo
  // interpreté» en `Mensaje`. El cupo no depende de esto.
  it("sin modo debug la línea de métricas no existe en el DOM", () => {
    render(<FranjaDeEstado enVuelo={false} turnos={TURNOS} debug={false} />);

    expect(screen.queryByText(/consultas al modelo/)).toBeNull();
  });

  it("con modo debug prendido la línea de métricas aparece", () => {
    render(<FranjaDeEstado enVuelo={false} turnos={TURNOS} debug />);

    expect(screen.getByText(/consultas al modelo/)).toBeInTheDocument();
  });
});
