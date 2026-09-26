import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { Mensaje } from "./components/Mensaje";
import { Sugerencias } from "./components/Sugerencias";
import { montar, respuesta } from "./test/soporte";
import type { TurnoDeLaConversacion } from "./types";

// ============================================================
// Follow-up suggestions after a SUCCESSFUL turn render the same way a
// rejection's suggestions already did (asistente-contrato-de-respuesta,
// asistente-superficie-frontend). `Sugerencias.tsx` itself is already
// state-agnostic — it only looks at the list it is given — so what this file
// mostly proves is that `Mensaje.tsx` keeps wiring the success path's
// `sugerencias` field through it exactly like the rejection path's.
// ============================================================

function montarMensaje(unTurno: TurnoDeLaConversacion, onElegir = vi.fn()) {
  montar(
    <ul>
      <Mensaje turno={unTurno} onElegir={onElegir} onReintentar={() => {}} enVuelo={false} />
    </ul>,
  );
  return onElegir;
}

describe("Sugerencias, in isolation", () => {
  it("renders nothing without any suggestion", () => {
    const { container } = montar(
      <Sugerencias sugerencias={[]} onElegir={() => {}} deshabilitado={false} />,
    );

    expect(container.querySelector(".adoc-asistente-sugerencias")).toBeNull();
  });

  it("renders one chip per suggestion", () => {
    montar(
      <Sugerencias
        sugerencias={["¿Qué carreras están vigentes?", "¿Cuántos pedidos hay?"]}
        onElegir={() => {}}
        deshabilitado={false}
      />,
    );

    expect(
      screen.getByRole("button", { name: "¿Qué carreras están vigentes?" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "¿Cuántos pedidos hay?" })).toBeInTheDocument();
  });
});

describe("A successfully answered turn's suggestions", () => {
  it("render the same way a rejection's suggestions do", () => {
    const conExito = respuesta({
      estado: "respondida",
      sugerencias: ["¿Qué materias no tienen ningún docente designado?"],
    });
    const conRechazo = respuesta({
      estado: "no_contestable",
      sugerencias: ["¿Qué materias no tienen ningún docente designado?"],
    });

    const { unmount } = montar(
      <ul>
        <Mensaje
          turno={{ id: "t-exito", pregunta: "x", respuesta: conExito }}
          onElegir={() => {}}
          onReintentar={() => {}}
          enVuelo={false}
        />
      </ul>,
    );
    const marcadoDeExito = screen.getByRole("button", {
      name: "¿Qué materias no tienen ningún docente designado?",
    }).outerHTML;
    unmount();

    montar(
      <ul>
        <Mensaje
          turno={{ id: "t-rechazo", pregunta: "x", respuesta: conRechazo }}
          onElegir={() => {}}
          onReintentar={() => {}}
          enVuelo={false}
        />
      </ul>,
    );
    const marcadoDeRechazo = screen.getByRole("button", {
      name: "¿Qué materias no tienen ningún docente designado?",
    }).outerHTML;

    expect(marcadoDeExito).toBe(marcadoDeRechazo);
  });

  it("renders no suggestions section when the successful turn has none", () => {
    montarMensaje({
      id: "t-1",
      pregunta: "x",
      respuesta: respuesta({ estado: "respondida", sugerencias: [] }),
    });

    expect(screen.queryByText("Probá con alguna de estas:")).toBeNull();
  });

  it("choosing a post-success suggestion starts a new question, same as a post-rejection one", async () => {
    const user = userEvent.setup();
    const onElegir = montarMensaje({
      id: "t-1",
      pregunta: "x",
      respuesta: respuesta({
        estado: "respondida",
        sugerencias: ["¿Cómo se compone el plantel por cargo?"],
      }),
    });

    await user.click(
      screen.getByRole("button", { name: "¿Cómo se compone el plantel por cargo?" }),
    );

    expect(onElegir).toHaveBeenCalledWith("¿Cómo se compone el plantel por cargo?");
  });
});
