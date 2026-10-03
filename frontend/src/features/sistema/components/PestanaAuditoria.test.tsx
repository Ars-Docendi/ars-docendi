import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";

import { PestanaAuditoria } from "./PestanaAuditoria";
import * as api from "../api/auditoriaApi";
import type { EventoAuditoria, PaginaAuditoria } from "../api/auditoriaApi";
import { limiteDesdeParaPeriodo } from "../utils/zonaHoraria";

afterEach(() => vi.restoreAllMocks());

function evento(sobrescribir: Partial<EventoAuditoria>): EventoAuditoria {
  return {
    id: "cambios-1",
    origen: "cambios",
    schema: "identity",
    tabla: "users",
    rowPk: "1042",
    accion: "UPDATE",
    cambiadoEn: new Date().toISOString(),
    cambiadoPor: null,
    requestId: null,
    columnasCambiadas: [],
    cambios: [],
    actor: "Lucía Fernández",
    tipoActor: "persona",
    accionEtiqueta: "Cambio",
    modulo: "Identidad",
    objeto: "Solicitud #1042",
    resumen: "Solicitud #1042: Estado pendiente → aprobado",
    ...sobrescribir,
  };
}

function pagina(
  elementos: EventoAuditoria[],
  extra: Partial<PaginaAuditoria> = {},
): PaginaAuditoria {
  return {
    elementos,
    pagina: 1,
    tamanoPagina: 50,
    total: elementos.length,
    parcial: false,
    fuentesNoDisponibles: [],
    ...extra,
  };
}

function montar(entrada = "/sistema#auditoria") {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[entrada]}>
        <PestanaAuditoria />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("PestanaAuditoria", () => {
  it("sin query string, pide los últimos 7 días", async () => {
    const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([]));

    montar();

    await waitFor(() => expect(espia).toHaveBeenCalled());
    const filtrosEnviados = espia.mock.calls[0][0];
    expect(filtrosEnviados.desde).toBe(limiteDesdeParaPeriodo("7d"));
  });

  it("cambiar un chip actualiza la URL y dispara una nueva consulta", async () => {
    const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([]));
    const user = userEvent.setup();

    montar();
    await waitFor(() => expect(espia).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole("button", { name: "Cambios" }));

    await waitFor(() => expect(espia).toHaveBeenCalledTimes(2));
    expect(espia.mock.calls[1][0]).toMatchObject({ accion: "UPDATE" });
  });

  it("abrir y cerrar el detalle por la misma fila, y por «×»", async () => {
    vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));
    const user = userEvent.setup();

    montar();

    const fila = await screen.findByRole("button", {
      name: /Solicitud #1042: Estado pendiente → aprobado/,
    });
    await user.click(fila);

    expect(await screen.findByRole("button", { name: "Cerrar detalle" })).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    await user.click(fila);
    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Cerrar detalle" })).not.toBeInTheDocument(),
    );

    await user.click(fila);
    await screen.findByRole("button", { name: "Cerrar detalle" });
    await user.click(screen.getByRole("button", { name: "Cerrar detalle" }));
    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Cerrar detalle" })).not.toBeInTheDocument(),
    );
  });

  it("Escape cierra el detalle y devuelve el foco a la fila que lo abrió (ARS-160)", async () => {
    vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));
    const user = userEvent.setup();

    montar();
    const fila = await screen.findByRole("button", {
      name: /Solicitud #1042: Estado pendiente → aprobado/,
    });
    await user.click(fila);
    await screen.findByRole("button", { name: "Cerrar detalle" });

    await user.keyboard("{Escape}");

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Cerrar detalle" })).not.toBeInTheDocument(),
    );
    await waitFor(() => expect(document.activeElement).toBe(fila));
  });

  it("cambiar el filtro de acción de modo que el evento ya no matchee cierra el panel", async () => {
    const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));
    const user = userEvent.setup();

    montar();
    const fila = await screen.findByRole("button", {
      name: /Solicitud #1042: Estado pendiente → aprobado/,
    });
    await user.click(fila);
    await screen.findByRole("button", { name: "Cerrar detalle" });

    espia.mockResolvedValue(pagina([]));
    await user.click(screen.getByRole("button", { name: "Eliminaciones" }));

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Cerrar detalle" })).not.toBeInTheDocument(),
    );
  });
});
