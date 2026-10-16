import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { CambiosRecientes } from "./CambiosRecientes";
import * as api from "../api/auditoriaApi";
import type { EventoAuditoria, PaginaAuditoria } from "../api/auditoriaApi";

afterEach(() => vi.restoreAllMocks());

function evento(sobrescribir: Partial<EventoAuditoria>): EventoAuditoria {
  return {
    id: "cambios-1",
    origen: "cambios",
    schema: "identity",
    tabla: "users",
    rowPk: "1042",
    accion: "UPDATE",
    cambiadoEn: "2026-09-26T22:10:00.000Z",
    cambiadoPor: null,
    requestId: null,
    columnasCambiadas: [],
    cambios: [],
    actor: "Ernesto Vidal",
    tipoActor: "persona",
    accionEtiqueta: "Cambio",
    modulo: "Identidad",
    objeto: "Solicitud #1042",
    resumen: "Solicitud #1042: Estado pendiente → aprobado",
    ...sobrescribir,
  };
}

function pagina(elementos: EventoAuditoria[]): PaginaAuditoria {
  return {
    elementos,
    pagina: 1,
    tamanoPagina: 4,
    total: elementos.length,
    parcial: false,
    fuentesNoDisponibles: [],
  };
}

function montar(onAbrirCambio = vi.fn(), onVerTodo = vi.fn()) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return {
    onAbrirCambio,
    onVerTodo,
    ...render(
      <QueryClientProvider client={cliente}>
        <CambiosRecientes onAbrirCambio={onAbrirCambio} onVerTodo={onVerTodo} />
      </QueryClientProvider>,
    ),
  };
}

describe("CambiosRecientes", () => {
  it("pide los últimos 4 eventos sin fechas y los lista con hora, resumen, actor y acción", async () => {
    const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));

    montar();

    expect(
      await screen.findByText("Solicitud #1042: Estado pendiente → aprobado"),
    ).toBeInTheDocument();
    expect(espia).toHaveBeenCalledWith({ pagina: 1, tamanoPagina: 4 });
    expect(screen.getByText(/Ernesto Vidal/)).toBeInTheDocument();
    expect(screen.getByText("Cambio")).toBeInTheDocument();
  });

  it("activar una fila abre su detalle", async () => {
    vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({ id: "cambios-7" })]));
    const user = userEvent.setup();
    const { onAbrirCambio } = montar();

    const fila = await screen.findByRole("button", {
      name: /Solicitud #1042: Estado pendiente → aprobado/,
    });
    await user.click(fila);

    expect(onAbrirCambio).toHaveBeenCalledWith("cambios-7");
  });

  it("«Ver todo en Auditoría →» dispara su callback", async () => {
    vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([]));
    const user = userEvent.setup();
    const { onVerTodo } = montar();

    await waitFor(() => expect(screen.getByText(/Todavía no hay cambios/)).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: "Ver todo en Auditoría →" }));

    expect(onVerTodo).toHaveBeenCalled();
  });

  it("sin eventos muestra el estado vacío en vez de una lista", async () => {
    vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([]));

    montar();

    expect(await screen.findByText("Todavía no hay cambios registrados.")).toBeInTheDocument();
  });
});
