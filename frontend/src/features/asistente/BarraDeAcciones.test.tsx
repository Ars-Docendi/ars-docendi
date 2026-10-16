/// <reference types="node" />
import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { hojaDeLaFeature, sinComentarios as sinComentariosDe } from "./test/hojas";
import { BarraDeAcciones } from "./components/BarraDeAcciones";
import { montar } from "./test/soporte";

// ============================================================
// La barra de acciones bajo cada respuesta (ARS-146, §5 de
// asistente-rediseno-v3): reemplaza `AccionesDelMensaje` y los botones de
// texto de `VotoDeRetroalimentacion`/la tabla, con íconos con nombre y
// tooltip, y las reglas de visibilidad de design.md D6.
// ============================================================

const TOKEN = "11111111-1111-4111-8111-111111111111";
const hoja = hojaDeLaFeature();
const sinComentarios = sinComentariosDe(hoja);

function ref(): { current: HTMLButtonElement | null } {
  return { current: null };
}

describe("Qué controles existen", () => {
  it("con tabla y token, muestra la barra completa", () => {
    userEvent.setup();
    montar(
      <BarraDeAcciones
        texto="Hay 4 docentes."
        onExportar={() => {}}
        onAmpliar={() => {}}
        ampliarBotonRef={ref()}
        claveDeRetroalimentacion={TOKEN}
      />,
    );

    expect(screen.getByRole("button", { name: "Copiar respuesta" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ampliar tabla" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Exportar a CSV" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sirvió" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "No sirvió" })).toBeInTheDocument();
  });

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

  it("sin ninguna de las tres condiciones, no se monta nada", () => {
    const original = Object.getOwnPropertyDescriptor(navigator, "clipboard");
    Object.defineProperty(navigator, "clipboard", { value: undefined, configurable: true });

    try {
      const { container } = montar(
        <BarraDeAcciones
          texto="Hay 4 docentes."
          onExportar={null}
          onAmpliar={() => {}}
          ampliarBotonRef={ref()}
          claveDeRetroalimentacion={null}
        />,
      );

      expect(container.textContent).toBe("");
    } finally {
      if (original) Object.defineProperty(navigator, "clipboard", original);
      else Reflect.deleteProperty(navigator, "clipboard");
    }
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

describe("aria-pressed en el voto", () => {
  it("«Sirvió» y «No sirvió» arrancan sin presionar", () => {
    montar(
      <BarraDeAcciones
        texto="Hay 4 docentes."
        onExportar={() => {}}
        onAmpliar={() => {}}
        ampliarBotonRef={ref()}
        claveDeRetroalimentacion={TOKEN}
      />,
    );

    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "No sirvió" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });
});

// ============================================================
// Visibilidad de la barra (design.md D6): siempre en el DOM y en el orden de
// Tab, opacidad-only, revelada por hover/foco del turno, y forzada en el
// último turno y en los votados. jsdom no calcula layout ni pseudo-clases: la
// regla se fija como texto, igual que ya hace `TablaDeResultado.test.tsx`
// para el «⇅» de orden.
// ============================================================

describe("Visibilidad de la barra (design.md D6)", () => {
  it("la barra arranca apagada", () => {
    const regla = sinComentarios.match(/\.adoc-asistente-barra\s*\{([^}]*)\}/);
    expect(regla).not.toBeNull();
    expect(regla?.[1]).toMatch(/opacity:\s*0;/);
  });

  it("se revela con el foco dentro del turno, con el hover, y con el último turno o uno votado", () => {
    const indiceRegla = sinComentarios.indexOf(
      ".adoc-asistente-turno--ultimo .adoc-asistente-barra",
    );
    expect(indiceRegla).toBeGreaterThan(-1);

    const bloque = sinComentarios.slice(indiceRegla, indiceRegla + 400);
    expect(bloque).toMatch(/\.adoc-asistente-turno:hover \.adoc-asistente-barra/);
    expect(bloque).toMatch(/\.adoc-asistente-turno:focus-within \.adoc-asistente-barra/);
    expect(bloque).toMatch(
      /\.adoc-asistente-turno:has\(\[aria-pressed="true"\]\) \.adoc-asistente-barra/,
    );
    expect(bloque).toMatch(/opacity:\s*1;/);
  });

  it("los controles de la barra no dependen de `display`: quedan en el DOM aunque apagados", () => {
    // Cubierto también funcionalmente: los tests de arriba usan `getByRole`
    // (que exige que el elemento esté en el árbol de accesibilidad) sin abrir
    // hover ni foco del turno, así que ya prueban que la barra no depende de
    // una interacción para existir en el DOM.
    expect(sinComentarios).not.toMatch(/\.adoc-asistente-barra\s*\{[^}]*display:\s*none/);
  });
});
