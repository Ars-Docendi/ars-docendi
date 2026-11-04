/// <reference types="node" />
import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { BarraDeAcciones } from "./components/BarraDeAcciones";
import { montar } from "./test/soporte";

// ============================================================
// La barra de acciones bajo cada respuesta (ARS-146, §5 de
// asistente-rediseno-v3): reemplaza `AccionesDelMensaje` y los botones de
// texto de `VotoDeRetroalimentacion`/la tabla, con íconos con nombre y
// tooltip, y las reglas de visibilidad de design.md D6.
// ============================================================

const TOKEN = "11111111-1111-4111-8111-111111111111";

function ref(): { current: HTMLButtonElement | null } {
  return { current: null };
}

describe("Qué controles existen", () => {
  it("sin filas (onExportar en null) no hay «Ampliar tabla» ni «Exportar a CSV»", () => {
    userEvent.setup();
    montar(
      <BarraDeAcciones
        texto="Hay 4 docentes."
        onExportar={null}
        onAmpliar={() => {}}
        ampliarBotonRef={ref()}
        claveDeRetroalimentacion={TOKEN}
      />,
    );

    expect(screen.queryByRole("button", { name: "Ampliar tabla" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Exportar a CSV" })).toBeNull();
    // El resto de la barra sigue: la tabla es sólo una de las tres razones de
    // existir (asistente-superficie-frontend).
    expect(screen.getByRole("button", { name: "Copiar respuesta" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sirvió" })).toBeInTheDocument();
  });

  it("sin token no hay botones de voto", () => {
    montar(
      <BarraDeAcciones
        texto="Hay 4 docentes."
        onExportar={() => {}}
        onAmpliar={() => {}}
        ampliarBotonRef={ref()}
        claveDeRetroalimentacion={null}
      />,
    );

    expect(screen.queryByRole("button", { name: "Sirvió" })).toBeNull();
    expect(screen.queryByRole("button", { name: "No sirvió" })).toBeNull();
  });

  it("«Exportar a CSV» llama a la función ya resuelta que le llega", async () => {
    const exportar = vi.fn();
    const user = userEvent.setup();
    montar(
      <BarraDeAcciones
        texto="Hay 4 docentes."
        onExportar={exportar}
        onAmpliar={() => {}}
        ampliarBotonRef={ref()}
        claveDeRetroalimentacion={TOKEN}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Exportar a CSV" }));

    expect(exportar).toHaveBeenCalledTimes(1);
  });

  it("«Ampliar tabla» llama a `onAmpliar`", async () => {
    const onAmpliar = vi.fn();
    const user = userEvent.setup();
    montar(
      <BarraDeAcciones
        texto="Hay 4 docentes."
        onExportar={() => {}}
        onAmpliar={onAmpliar}
        ampliarBotonRef={ref()}
        claveDeRetroalimentacion={TOKEN}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Ampliar tabla" }));

    expect(onAmpliar).toHaveBeenCalledTimes(1);
  });
});

// ============================================================
// Visibilidad de la barra (design.md D6): siempre en el DOM y en el orden de
// Tab, opacidad-only, revelada por hover/foco del turno, y forzada en el
// último turno y en los votados. jsdom no calcula layout ni pseudo-clases: la
// regla se fija como texto, igual que ya hace `TablaDeResultado.test.tsx`
// para el «⇅» de orden.
// ============================================================
