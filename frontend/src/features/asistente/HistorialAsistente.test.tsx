import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { LanzadorAsistente } from "./components/LanzadorAsistente";
import { PanelDePrueba } from "./test/PanelDePrueba";
import * as api from "./api/asistenteApi";
import * as historialApi from "./api/historialApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import type { ConversacionResumen, ReejecucionResultado, TurnoDeHistorial } from "./types";

// ============================================================
// Reanudar una conversación propia y volver a consultar un turno ya
// respondido (asistente-historial-conversaciones, tasks.md §10.3, §12).
// ============================================================

const CONVERSACION: ConversacionResumen = {
  id: "33333333-3333-4333-8333-333333333333",
  titulo: "¿Cuántos docentes hay?",
  creadoEn: "2026-01-10T10:00:00Z",
  ultimaActividad: "2026-01-10T10:05:00Z",
};

const TURNO_RESPONDIDO: TurnoDeHistorial = {
  id: "44444444-4444-4444-8444-444444444444",
  pregunta: "¿Cuántos docentes hay?",
  sql: "SELECT count(*) FROM identity.personas",
  estado: "respondida",
  ocurrioEn: "2026-01-10T10:00:05Z",
};

const HILO_EFIMERO_NUEVO = "55555555-5555-4555-8555-555555555555";

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

// El título de una conversación es la pregunta que la abrió (auto-titulado
// desde el backend): a propósito, `CONVERSACION.titulo` y
// `TURNO_RESPONDIDO.pregunta` son el MISMO texto acá, igual que en un caso
// real. Desde que el rail y el encabezado quedan siempre a la vista (v3), ese
// texto aparece en tres lugares a la vez —la fila del rail, el título del
// encabezado y la burbuja de la pregunta—, así que cualquier aserción sobre
// la burbuja necesita acotarse a la región viva de la conversación.
function regionViva(): HTMLElement {
  return screen.getByRole("log", { name: "Conversación con el asistente" });
}

describe("Reachable desde los dos montajes (tasks.md 10.3)", () => {
  it("el rail está en la ruta (PanelDePrueba)", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([]);
    montar(<PanelDePrueba />);

    expect(
      await screen.findByRole("button", { name: "Colapsar conversaciones" }),
    ).toBeInTheDocument();
  });

  it("el mismo rail está en el modal del lanzador", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([]);
    const user = userEvent.setup();
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));

    expect(
      await screen.findByRole("button", { name: "Colapsar conversaciones" }),
    ).toBeInTheDocument();
  });
});

describe("Reanudar una conversación (tasks.md 12.1, 12.2)", () => {
  it("abrirla llama a reanudar, muestra sus turnos y guarda el hilo efímero nuevo para el próximo pedido", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([CONVERSACION]);
    vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
      hilo: HILO_EFIMERO_NUEVO,
      turnos: [TURNO_RESPONDIDO],
    });
    const consultarSpy = vi
      .spyOn(api, "consultar")
      .mockResolvedValue(respuesta({ hilo: HILO_EFIMERO_NUEVO, respuesta: "Hay 12 docentes." }));
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await user.click(await screen.findByText(CONVERSACION.titulo));

    // Se ve el turno restaurado: la pregunta y su desenlace.
    expect(await within(regionViva()).findByText(TURNO_RESPONDIDO.pregunta)).toBeInTheDocument();
    expect(screen.getByText("Esta pregunta fue respondida.")).toBeInTheDocument();

    // Un seguimiento manda el hilo efímero NUEVO, no el viejo ni ninguno.
    await user.type(screen.getByLabelText("Tu pregunta"), "¿y de qué carreras?{Enter}");

    await waitFor(() =>
      expect(consultarSpy).toHaveBeenCalledWith(
        expect.objectContaining({ hilo: HILO_EFIMERO_NUEVO }),
        expect.anything(),
        expect.anything(),
      ),
    );

    // Y ese seguimiento aparece VISIBLEMENTE en la misma conversación, junto
    // al turno restaurado.
    expect(await screen.findByText("Hay 12 docentes.")).toBeInTheDocument();
    expect(within(regionViva()).getByText(TURNO_RESPONDIDO.pregunta)).toBeInTheDocument();
  });

  it("la fila de la conversación reanudada queda marcada con `aria-current`", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([CONVERSACION]);
    vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
      hilo: HILO_EFIMERO_NUEVO,
      turnos: [TURNO_RESPONDIDO],
    });
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await user.click(await screen.findByText(CONVERSACION.titulo));
    await within(regionViva()).findByText(TURNO_RESPONDIDO.pregunta);

    // El rail sigue a la vista —ya no hay un panel que abrir y cerrar—: la
    // fila de la conversación que quedó activa en el hilo se resalta, para
    // que se vea cuál es «esta» entre las demás.
    expect(screen.getByRole("button", { name: CONVERSACION.titulo })).toHaveAttribute(
      "aria-current",
      "true",
    );
  });

  it("reanudar la conversación de otra persona no es una acción que este panel ofrezca", async () => {
    // No hay ningún control en este panel que pida un id ajeno: reanudar
    // sólo se dispara sobre lo que trajo `GET /historial`, que ya está
    // acotado al actor de la sesión (backend, tasks.md 7.4).
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([CONVERSACION]);

    montar(<PanelDePrueba />);

    expect(await screen.findByText(CONVERSACION.titulo)).toBeInTheDocument();
    expect(screen.queryByLabelText(/id de otra conversación/i)).toBeNull();
  });
});

describe("«Volver a consultar» sobre un turno restaurado (tasks.md 12.3, 12.4, 14.3)", () => {
  async function reanudarConversacion() {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([CONVERSACION]);
    vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
      hilo: HILO_EFIMERO_NUEVO,
      turnos: [TURNO_RESPONDIDO],
    });
    const user = userEvent.setup();
    montar(<PanelDePrueba />);
    await user.click(await screen.findByText(CONVERSACION.titulo));
    await within(regionViva()).findByText(TURNO_RESPONDIDO.pregunta);
    return user;
  }

  it("está presente en un turno respondido y ausente en cualquier otro desenlace", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([CONVERSACION]);
    vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
      hilo: HILO_EFIMERO_NUEVO,
      turnos: [
        TURNO_RESPONDIDO,
        { ...TURNO_RESPONDIDO, id: "t-2", estado: "no_contestable", sql: null },
        { ...TURNO_RESPONDIDO, id: "t-3", estado: "necesita_aclaracion", sql: null },
        { ...TURNO_RESPONDIDO, id: "t-4", estado: "servicio_degradado", sql: null },
      ],
    });
    const user = userEvent.setup();
    montar(<PanelDePrueba />);
    await user.click(await screen.findByText(CONVERSACION.titulo));
    await screen.findByText("El servicio estaba degradado cuando se hizo esta pregunta.");

    expect(screen.getAllByRole("button", { name: "Volver a consultar" })).toHaveLength(1);
  });

  it("al activarla llama a la re-ejecución y renderiza la tabla devuelta", async () => {
    const user = await reanudarConversacion();
    const RESULTADO: ReejecucionResultado = {
      exitosa: true,
      columnas: [{ nombre: "apellido", sensible: false }],
      filas: [["Gómez"]],
      truncado: false,
    };
    const reejecutar = vi.spyOn(historialApi, "reejecutarTurno").mockResolvedValue(RESULTADO);

    await user.click(screen.getByRole("button", { name: "Volver a consultar" }));

    expect(reejecutar).toHaveBeenCalledWith(TURNO_RESPONDIDO.id);
    expect(await screen.findByText("Gómez")).toBeInTheDocument();
    expect(screen.getByText("Consulta actualizada.")).toBeInTheDocument();
  });

  it("una re-ejecución que falla con gracia muestra el mensaje, sin tabla ni caída visible", async () => {
    const user = await reanudarConversacion();
    vi.spyOn(historialApi, "reejecutarTurno").mockResolvedValue({
      exitosa: false,
      mensaje:
        "No pude volver a ejecutar esa consulta. Puede que ya no tengas acceso a esos datos.",
      columnas: [],
      filas: [],
      truncado: false,
    });

    await user.click(screen.getByRole("button", { name: "Volver a consultar" }));

    expect(
      await screen.findByText(
        "No pude volver a ejecutar esa consulta. Puede que ya no tengas acceso a esos datos.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("es operable por teclado y anuncia su desenlace por la región viva existente", async () => {
    const user = await reanudarConversacion();
    vi.spyOn(historialApi, "reejecutarTurno").mockResolvedValue({
      exitosa: true,
      columnas: [{ nombre: "apellido", sensible: false }],
      filas: [["Gómez"]],
      truncado: false,
    });

    const boton = screen.getByRole("button", { name: "Volver a consultar" });
    boton.focus();
    await user.keyboard("{Enter}");

    // La misma región viva de siempre: `Conversacion.tsx`, `role="log"
    // aria-live="polite"`. Nada de esto abre una segunda.
    const region = screen.getByRole("log", { name: "Conversación con el asistente" });
    expect(await screen.findByText("Consulta actualizada.")).toBeInTheDocument();
    expect(region.contains(screen.getByText("Consulta actualizada."))).toBe(true);
    // El foco no se movió del botón que se activó.
    expect(boton).toHaveFocus();
  });
});
