import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { Mensaje } from "./components/Mensaje";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { RespuestaDelAsistente, TurnoDeLaConversacion } from "./types";

// ============================================================
// Un mensaje de la conversación: el razonamiento colapsado y lo que nunca se
// muestra.
//
// El backend redacta `razonamiento` para el usuario final —una o dos oraciones en
// español, sin nombres de tablas ni de columnas— y lo omite cuando está vacío. Va
// dentro del mensaje como un `<details>` cerrado: es parte de la respuesta, así
// que vive en la región viva, pero el contenido de una disclosure cerrada no se
// anuncia hasta abrirla, y por eso no le agrega ruido al lector.
// `preguntaInterpretada` queda visible y afuera: es el aviso de que la pregunta se
// reinterpretó, y esconderlo derrota su razón de ser (RF-10).
// ============================================================

const RAZONAMIENTO = "Busqué los docentes con designación vigente.";

function turno(parcial: Partial<RespuestaDelAsistente> = {}): TurnoDeLaConversacion {
  return { id: "t-1", pregunta: "¿cuántos docentes hay?", respuesta: respuesta(parcial) };
}

function montarMensaje(unTurno: TurnoDeLaConversacion) {
  return montar(
    <ul>
      <Mensaje turno={unTurno} onElegir={() => {}} onReintentar={() => {}} enVuelo={false} />
    </ul>,
  );
}

describe("El razonamiento", () => {
  it("con razonamiento hay una disclosure «Cómo lo interpreté», cerrada", () => {
    montarMensaje(turno({ razonamiento: RAZONAMIENTO }));

    const resumen = screen.getByText("Cómo lo interpreté");
    expect(resumen.tagName).toBe("SUMMARY");

    const disclosure = resumen.closest("details");
    expect(disclosure).not.toBeNull();
    expect(disclosure).not.toHaveAttribute("open");
    // Cerrada: el texto está en el DOM, pero no se ve ni se anuncia hasta abrirla.
    expect(screen.getByText(RAZONAMIENTO)).not.toBeVisible();
  });

  it("al abrirla se lee el razonamiento", async () => {
    const user = userEvent.setup();
    montarMensaje(turno({ razonamiento: RAZONAMIENTO }));

    await user.click(screen.getByText("Cómo lo interpreté"));

    expect(screen.getByText(RAZONAMIENTO)).toBeVisible();
  });

  it("sin razonamiento no hay disclosure", () => {
    montarMensaje(turno());

    expect(screen.queryByText("Cómo lo interpreté")).toBeNull();
  });

  it("«Entendí:» queda visible, fuera de la disclosure", () => {
    montarMensaje(
      turno({
        preguntaInterpretada: "¿Cuántos docentes tienen designación vigente?",
        razonamiento: RAZONAMIENTO,
      }),
    );

    const entendi = screen.getByText(/Entendí:/);
    expect(entendi).toBeVisible();
    expect(screen.getByText("¿Cuántos docentes tienen designación vigente?")).toBeVisible();

    const disclosure = screen.getByText("Cómo lo interpreté").closest("details");
    expect(disclosure?.contains(entendi)).toBe(false);
  });
});

describe("Lo que nunca se muestra", () => {
  beforeEach(() => {
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("ninguna etiqueta interna llega al texto de la página", async () => {
    // Lo que el backend manda de verdad: `categoria` sigue viajando aunque el tipo
    // del cliente ya no la declare. Es la etiqueta interna del carril que resolvió
    // el turno (`consulta_simple`, `cruce_de_tablas`) y RNF-18 la prohíbe; lo
    // mismo el `estado` crudo y el nombre interno de las áreas del catálogo, que
    // la fixture trae a propósito para que este test muerda.
    const METRICAS_DEL_BACKEND = { llamadasAlModelo: 2, categoria: "consulta_simple" };
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        estado: "respondida",
        razonamiento: "Conté las designaciones vigentes.",
        metricas: METRICAS_DEL_BACKEND,
      }),
    );
    montar(<PanelDePrueba />);

    await screen.findByText(/áreas de datos del sistema/);
    expect(document.body.textContent).not.toMatch(/designaciones\.|identity\./);

    await user.type(screen.getByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(document.body.textContent).not.toMatch(
      /consulta_simple|respondida|designaciones\.|identity\./,
    );
  });
});
