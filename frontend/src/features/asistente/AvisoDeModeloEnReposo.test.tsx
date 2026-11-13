import { describe, it, expect, vi, afterEach } from "vitest";
import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDePrueba } from "./test/PanelDePrueba";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import type { DisponibilidadDelModelo } from "./types";

// ============================================================
// El aviso del modelo en reposo en el panel (asistente-modelo-en-reposo, tasks.md 4.1).
// ============================================================

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

const EN_REPOSO = "El modelo está en reposo. La primera respuesta puede demorar unos segundos.";
const FALLO = "No se pudo activar el modelo. Probá de nuevo.";

function disponibilidad(parcial: Partial<DisponibilidadDelModelo>): DisponibilidadDelModelo {
  return { estado: "en_reposo", ultimaActivacionFallida: false, ...parcial };
}

function conModelo(...lecturas: DisponibilidadDelModelo[]) {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
  const consulta = vi.spyOn(api, "obtenerDisponibilidadDelModelo");
  lecturas.forEach((l) => consulta.mockResolvedValueOnce(l));
  consulta.mockResolvedValue(lecturas[lecturas.length - 1]);
  return consulta;
}

describe("Cuándo se muestra el aviso", () => {
  it("con el modelo en reposo dice el texto exacto y ofrece «Activar modelo», en una región viva cortés", async () => {
    conModelo(disponibilidad({}));

    montar(<PanelDePrueba />);

    const texto = await screen.findByText(EN_REPOSO);
    expect(texto.closest('[aria-live="polite"]')).not.toBeNull();
    expect(screen.getByRole("button", { name: "Activar modelo" })).toBeEnabled();
  });

  it.each(["no_aplica", "listo", "desconocido"] as const)(
    "con el estado %s no aparece ni el texto ni el botón",
    async (estado) => {
      const consulta = conModelo(disponibilidad({ estado }));

      montar(<PanelDePrueba />);
      await waitFor(() => expect(consulta).toHaveBeenCalled());
      await screen.findByLabelText("Tu pregunta");

      expect(screen.queryByText(EN_REPOSO)).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Activar modelo" })).not.toBeInTheDocument();
    },
  );

  it("en mantenimiento no pregunta ni muestra el aviso", async () => {
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue({
      ...CAPACIDADES,
      mantenimiento: { activo: true, razon: "actualización" },
    });
    const consulta = vi
      .spyOn(api, "obtenerDisponibilidadDelModelo")
      .mockResolvedValue(disponibilidad({}));

    montar(<PanelDePrueba />);
    await screen.findByText(/El asistente está en mantenimiento/);

    expect(consulta).not.toHaveBeenCalled();
    expect(screen.queryByText(EN_REPOSO)).not.toBeInTheDocument();
  });

  it("una respuesta de la conversación lo quita: prueba que el modelo está cargado", async () => {
    conModelo(disponibilidad({}));
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    const usuario = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(EN_REPOSO);
    await usuario.type(screen.getByLabelText("Tu pregunta"), "¿Cuántos docentes hay?{Enter}");

    await screen.findByText("Hay 4 docentes designados.");
    expect(screen.queryByText(EN_REPOSO)).not.toBeInTheDocument();
  });
});

describe("Activar el modelo", () => {
  it("al pulsar muestra «Activando el modelo…» con el botón deshabilitado, luego «listo» y desaparece", async () => {
    conModelo(disponibilidad({}), disponibilidad({ estado: "listo" }));
    vi.spyOn(api, "activarModelo").mockResolvedValue(disponibilidad({ estado: "activando" }));
    const usuario = userEvent.setup();

    montar(<PanelDePrueba />);
    await usuario.click(await screen.findByRole("button", { name: "Activar modelo" }));

    expect(await screen.findByText("Activando el modelo…")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Activar modelo" })).toBeDisabled();

    // El sondeo (2 s) encuentra el modelo cargado.
    expect(
      await screen.findByText("El modelo está listo.", {}, { timeout: 5_000 }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Activar modelo" })).not.toBeInTheDocument();

    await waitFor(
      () => expect(screen.queryByText("El modelo está listo.")).not.toBeInTheDocument(),
      { timeout: 6_000 },
    );
  }, 15_000);

  it("si el servidor marca el último intento como fallido, lo dice y deja reintentar", async () => {
    conModelo(disponibilidad({}), disponibilidad({ ultimaActivacionFallida: true }));
    vi.spyOn(api, "activarModelo").mockResolvedValue(disponibilidad({ estado: "activando" }));
    const usuario = userEvent.setup();

    montar(<PanelDePrueba />);
    await usuario.click(await screen.findByRole("button", { name: "Activar modelo" }));

    expect(await screen.findByText(FALLO, {}, { timeout: 5_000 })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Activar modelo" })).toBeEnabled();
  }, 15_000);

  it("si el pedido de activación falla, dice que no se pudo", async () => {
    conModelo(disponibilidad({}));
    vi.spyOn(api, "activarModelo").mockRejectedValue(new Error("red"));
    const usuario = userEvent.setup();

    montar(<PanelDePrueba />);
    await usuario.click(await screen.findByRole("button", { name: "Activar modelo" }));

    expect(await screen.findByText(FALLO)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Activar modelo" })).toBeEnabled();
  });

  it("si el servidor sigue «activando» pasado el límite del cliente, lo da por fallido", async () => {
    conModelo(disponibilidad({}), disponibilidad({ estado: "activando" }));
    vi.spyOn(api, "activarModelo").mockResolvedValue(disponibilidad({ estado: "activando" }));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const usuario = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    montar(<PanelDePrueba />);
    await usuario.click(await screen.findByRole("button", { name: "Activar modelo" }));
    await screen.findByText("Activando el modelo…");

    await act(async () => {
      await vi.advanceTimersByTimeAsync(121_000);
    });

    expect(await screen.findByText(FALLO)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Activar modelo" })).toBeEnabled();
  });
});
