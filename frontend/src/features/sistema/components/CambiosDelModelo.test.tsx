import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import * as auditoriaApi from "../api/auditoriaApi";
import type { EventoAuditoria, PaginaAuditoria } from "../api/auditoriaApi";
import { CambiosDelModelo } from "./CambiosDelModelo";

function evento(id: string, resumen: string, cambiadoEn: string): EventoAuditoria {
  return {
    id,
    origen: "asistente",
    schema: "asistente",
    tabla: "modelo_del_asistente",
    rowPk: "—",
    accion: "UPDATE",
    cambiadoEn,
    cambiadoPor: null,
    requestId: null,
    columnasCambiadas: [],
    cambios: [],
    actor: "Admin Sistemas",
    tipoActor: "persona",
    accionEtiqueta: "Cambio",
    modulo: "Asistente",
    objeto: "Modelo del asistente",
    resumen,
  };
}

function pagina(elementos: EventoAuditoria[]): PaginaAuditoria {
  return {
    elementos,
    pagina: 1,
    tamanoPagina: 10,
    total: elementos.length,
    parcial: false,
    fuentesNoDisponibles: [],
  };
}

function montar(alAbrir = vi.fn(), alVerTodo = vi.fn()) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <CambiosDelModelo onAbrirCambio={alAbrir} onVerTodo={alVerTodo} />
    </QueryClientProvider>,
  );
  return { alAbrir, alVerTodo };
}

afterEach(() => vi.restoreAllMocks());

describe("CambiosDelModelo", () => {
  it("pide sólo los eventos del modelo y muestra el resumen del propio feed", async () => {
    vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue(
      pagina([
        evento(
          "asistente-2",
          "Clave de openai reemplazada: pista …ab12 → …cd34",
          "2026-10-22T10:00:00Z",
        ),
        evento(
          "asistente-1",
          "Modelo del asistente: configuración del servidor → openai / gpt-5",
          "2026-10-20T11:02:00Z",
        ),
      ]),
    );

    montar();

    const filas = await screen.findAllByRole("button", { name: /Modelo del asistente|Clave/ });
    expect(filas.map((f) => f.textContent)).toEqual([
      expect.stringContaining("Clave de openai reemplazada: pista …ab12 → …cd34"),
      expect.stringContaining("Modelo del asistente: configuración del servidor → openai / gpt-5"),
    ]);
    expect(auditoriaApi.listarAuditoria).toHaveBeenCalledOnce();
    expect(auditoriaApi.listarAuditoria).toHaveBeenCalledWith(
      expect.objectContaining({ modulo: "asistente", tabla: "modelo_del_asistente" }),
    );
    expect(auditoriaApi.listarAuditoria).not.toHaveBeenCalledWith(
      expect.objectContaining({ q: expect.anything() }),
    );
  });

  it("abrir una fila y «Ver todo» navegan a Auditoría", async () => {
    vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue(
      pagina([
        evento(
          "asistente-1",
          "Modelo del asistente: openai / a → openai / b",
          "2026-10-20T11:02:00Z",
        ),
      ]),
    );
    const user = userEvent.setup();
    const { alAbrir, alVerTodo } = montar();

    await user.click(await screen.findByRole("button", { name: /Modelo del asistente: openai/ }));
    await user.click(screen.getByRole("button", { name: /Ver todo en Auditoría/ }));

    expect(alAbrir).toHaveBeenCalledWith("asistente-1");
    expect(alVerTodo).toHaveBeenCalledOnce();
  });

  it("sin eventos del modelo lo dice, y si la carga falla ofrece reintentar", async () => {
    vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue(pagina([]));
    montar();
    expect(
      await screen.findByText("Todavía no hay cambios del modelo registrados."),
    ).toBeInTheDocument();
  });

  it("muestra un error y no un vacío cuando la auditoría falla", async () => {
    vi.spyOn(auditoriaApi, "listarAuditoria").mockRejectedValue(new Error("boom"));
    montar();
    expect(
      await screen.findByText("No se pudieron cargar los cambios del modelo."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reintentar" })).toBeInTheDocument();
  });
});
