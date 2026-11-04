import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";

import { PestanaAuditoria } from "./PestanaAuditoria";
import * as api from "../api/auditoriaApi";
import type { EventoAuditoria, PaginaAuditoria } from "../api/auditoriaApi";
import { datetimeLocalAIso, limiteDesdeParaPeriodo } from "../utils/zonaHoraria";

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

  it("escribir la tabla consulta una vez al terminar, no una vez por tecla", async () => {
    const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([]));
    const user = userEvent.setup();

    montar();
    await waitFor(() => expect(espia).toHaveBeenCalledTimes(1));
    await user.click(screen.getByRole("button", { name: /Más filtros/ }));
    await user.type(screen.getByRole("textbox", { name: "Tabla" }), "identity.users");

    await waitFor(() =>
      expect(espia).toHaveBeenLastCalledWith(expect.objectContaining({ tabla: "identity.users" })),
    );
    expect(espia).toHaveBeenCalledTimes(2);
  });

  it("explica clave compuesta y agrupación legada sin alterar el filtro JSON", async () => {
    const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([]));
    const user = userEvent.setup();
    const clave = '{"rol_id":"rol-sintetico","permiso_id":"permiso-sintetico"}';

    montar();
    await user.click(screen.getByRole("button", { name: /Más filtros/ }));
    const campo = screen.getByRole("textbox", { name: "Clave de fila" });
    expect(campo).toHaveAccessibleDescription(/JSON completo.*agrupación histórica/i);
    await user.click(campo);
    await user.paste(clave);

    await waitFor(() =>
      expect(espia).toHaveBeenLastCalledWith(expect.objectContaining({ rowPk: clave })),
    );
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

  describe("on a phone", () => {
    beforeEach(() => {
      // Phone viewport: `(max-width: 559px)` matches.
      vi.stubGlobal("matchMedia", (consulta: string) => ({
        matches: consulta.includes("559px"),
        addEventListener: () => {},
        removeEventListener: () => {},
      }));
    });
    afterEach(() => vi.unstubAllGlobals());

    it("applies action and a dd/mm/aaaa date from the filter sheet; a new date drops its hour", async () => {
      const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));
      const user = userEvent.setup();

      montar("/sistema?desde=2026-02-01T08:30#auditoria");
      await screen.findByText("Solicitud #1042: Estado pendiente → aprobado");

      await user.click(screen.getByRole("button", { name: /Filtros/ }));
      const desde = screen.getByRole("textbox", { name: "Desde" });
      expect(desde).toHaveValue("01/02/2026");
      await user.clear(desde);
      await user.type(desde, "15/03/2026");
      await user.click(screen.getByRole("checkbox", { name: "Eliminaciones" }));
      await user.click(screen.getByRole("button", { name: "Aplicar filtros" }));

      await waitFor(() =>
        expect(espia).toHaveBeenLastCalledWith(
          expect.objectContaining({
            accion: "DELETE",
            desde: datetimeLocalAIso("2026-03-15T00:00"),
          }),
        ),
      );
      expect(screen.getByRole("button", { name: "Quitar filtro Eliminaciones" })).toBeVisible();
    });

    it("keeps the typed search with spaces and queries it once the user pauses", async () => {
      const espia = vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));
      const user = userEvent.setup();

      montar();
      await screen.findByText("Solicitud #1042: Estado pendiente → aprobado");
      await user.type(screen.getByRole("searchbox", { name: "Buscar" }), "Lucía Fer");

      expect(screen.getByRole("searchbox", { name: "Buscar" })).toHaveValue("Lucía Fer");
      await waitFor(() =>
        expect(espia).toHaveBeenLastCalledWith(expect.objectContaining({ q: "Lucía Fer" })),
      );
    });

    it("opens the event detail right under its row and closing it returns focus to the row", async () => {
      vi.spyOn(api, "listarAuditoria").mockResolvedValue(pagina([evento({})]));
      const user = userEvent.setup();

      montar();
      const fila = await screen.findByRole("button", { name: /Estado pendiente → aprobado/ });
      await user.click(fila);

      const detalle = await screen.findByRole("complementary", { name: "Detalle del registro" });
      expect(fila.nextElementSibling).toBe(detalle);
      await user.click(screen.getByRole("button", { name: "Cerrar detalle" }));
      await waitFor(() => expect(fila).toHaveFocus());
    });
  });
});
