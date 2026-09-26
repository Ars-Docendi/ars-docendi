import { describe, it, expect, vi, afterEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDePrueba } from "./test/PanelDePrueba";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import type { CapacidadesDelAsistente } from "./types";

// ============================================================
// El cupo diario y el modo mantenimiento en la franja/panel del asistente
// (asistente-cupo-visible, asistente-modo-mantenimiento — tasks.md §12).
//
// Los cuatro estados y el panel base ya se prueban en `asistente.test.tsx`;
// este archivo cubre exclusivamente lo que agrega esta feature, con las
// mismas fixtures/montaje de `test/soporte` (asistente.test.tsx §comentario
// de cabecera de `LanzadorAsistente.test.tsx`).
// ============================================================

afterEach(() => {
  vi.restoreAllMocks();
});

function conCapacidades(parcial: Partial<CapacidadesDelAsistente>) {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue({ ...CAPACIDADES, ...parcial });
}

describe("El cupo restante en la franja de estado (tasks.md 12.1)", () => {
  it("se muestra fuera de la región viva de los turnos", async () => {
    conCapacidades({
      cupo: { restante: 13, bloqueado: false, motivo: null, vuelveA: null },
    });

    montar(<PanelDePrueba />);

    const indicador = await screen.findByText("Te quedan 13 consultas hoy.");
    expect(indicador.closest('[role="log"]')).toBeNull();
  });
});

describe("El estado bloqueado se transmite por texto (tasks.md 12.2)", () => {
  it("cupo propio agotado: dice el motivo, sin exponer un número de reintento", async () => {
    conCapacidades({
      cupo: {
        restante: 0,
        bloqueado: true,
        motivo: "presupuesto_propio",
        vuelveA: "2026-03-02T00:00:00Z",
      },
    });

    montar(<PanelDePrueba />);

    expect(await screen.findByText("Alcanzaste tu límite de hoy.")).toBeInTheDocument();
  });

  it("tope organizacional: no expone costo ni tope, sólo el texto genérico", async () => {
    conCapacidades({
      cupo: { restante: 0, bloqueado: true, motivo: "tope_organizacional", vuelveA: null },
    });

    montar(<PanelDePrueba />);

    const texto = await screen.findByText(
      "El asistente alcanzó el límite de uso de la organización.",
    );
    expect(texto.textContent).not.toMatch(/\$|USD|\d+([.,]\d+)?\s*(usd|dólar)/i);
  });
});

describe("El banner de mantenimiento (tasks.md 12.3)", () => {
  it("aparece con la razón y deshabilita el campo de entrada", async () => {
    conCapacidades({
      mantenimiento: { activo: true, razon: "Mantenimiento programado" },
      cupo: { restante: 0, bloqueado: true, motivo: "mantenimiento", vuelveA: null },
    });

    montar(<PanelDePrueba />);

    expect(
      await screen.findByText("El asistente está en mantenimiento: Mantenimiento programado."),
    ).toBeInTheDocument();
    expect(await screen.findByLabelText("Tu pregunta")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Enviar" })).toBeDisabled();
  });

  it("sin mantenimiento no aparece ningún banner y el campo queda habilitado", async () => {
    conCapacidades({});

    montar(<PanelDePrueba />);

    expect(screen.queryByText(/mantenimiento/i)).not.toBeInTheDocument();
    expect(await screen.findByLabelText("Tu pregunta")).not.toBeDisabled();
  });
});

describe("El cupo se actualiza desde la respuesta del turno (tasks.md 12.4)", () => {
  it("usa el cupoRestante del turno, no un refetch de capacidades", async () => {
    conCapacidades({
      cupo: { restante: 5, bloqueado: false, motivo: null, vuelveA: null },
    });
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta({ cupoRestante: 4 }));
    const user = userEvent.setup();

    montar(<PanelDePrueba />);

    expect(await screen.findByText("Te quedan 5 consultas hoy.")).toBeInTheDocument();

    await user.type(await screen.findByLabelText("Tu pregunta"), "¿cuántos docentes hay?{Enter}");

    expect(await screen.findByText("Te quedan 4 consultas hoy.")).toBeInTheDocument();
    expect(api.obtenerCapacidades).toHaveBeenCalledTimes(1);
  });
});

describe("La actualización del cupo no interrumpe el turno (tasks.md 12.5)", () => {
  it("no mueve el foco ni entra a la región viva del turno", async () => {
    conCapacidades({
      cupo: { restante: 5, bloqueado: false, motivo: null, vuelveA: null },
    });
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta({ cupoRestante: 4 }));
    const user = userEvent.setup();

    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, "¿cuántos docentes hay?{Enter}");

    const indicador = await screen.findByText("Te quedan 4 consultas hoy.");
    expect(indicador.closest('[role="log"]')).toBeNull();

    await waitFor(() => expect(document.activeElement).toBe(entrada));
  });
});

describe("El banner y el indicador se leen sin hover (tasks.md 13.3)", () => {
  it("ninguno de los dos depende de `title`", async () => {
    conCapacidades({
      mantenimiento: { activo: true, razon: "Mantenimiento programado" },
      cupo: { restante: 0, bloqueado: true, motivo: "mantenimiento", vuelveA: null },
    });

    montar(<PanelDePrueba />);

    const banner = await screen.findByText(
      "El asistente está en mantenimiento: Mantenimiento programado.",
    );
    expect(banner.closest("[title]")).toBeNull();
  });
});
