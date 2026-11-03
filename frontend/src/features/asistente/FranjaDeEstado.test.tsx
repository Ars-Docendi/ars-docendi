import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";

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
  // Decisión 14 del PO (2026-09-26): la línea de métricas sólo se muestra en modo debug
  // (`VITE_ASISTENTE_DEBUG`), el mismo switch que gatea «Cómo lo interpreté» en `Mensaje`.
  it("sin modo debug la línea de métricas no existe en el DOM", () => {
    render(<FranjaDeEstado enVuelo={false} turnos={TURNOS} debug={false} />);

    expect(screen.queryByText(/consultas al modelo/)).toBeNull();
  });

  it("con modo debug prendido la línea de métricas aparece", () => {
    render(<FranjaDeEstado enVuelo={false} turnos={TURNOS} debug />);

    expect(screen.getByText(/consultas al modelo/)).toBeInTheDocument();
  });
});
