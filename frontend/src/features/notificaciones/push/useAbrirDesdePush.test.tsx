import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({ marcarLeida: vi.fn() }));
vi.mock("../api/notificacionesApi", () => api);

import { CACHE_PENDIENTE, CLAVE_PENDIENTE } from "./pendientePush";
import { useAbrirDesdePush } from "./useAbrirDesdePush";

/** Minimal Cache Storage: one named cache of Responses. */
function instalarCaches(inicial?: unknown) {
  const almacen = new Map<string, Response>();
  if (inicial !== undefined) {
    almacen.set(CLAVE_PENDIENTE, new Response(JSON.stringify(inicial)));
  }
  vi.stubGlobal("caches", {
    open: async (nombre: string) => {
      expect(nombre).toBe(CACHE_PENDIENTE);
      return {
        match: async (clave: string) => almacen.get(clave)?.clone(),
        delete: async (clave: string) => almacen.delete(clave),
      };
    },
  });
  return almacen;
}

function Sonda() {
  useAbrirDesdePush();
  const { pathname } = useLocation();
  return <p data-testid="ruta">{pathname}</p>;
}

function renderizar() {
  render(
    <MemoryRouter initialEntries={["/portal"]}>
      <QueryClientProvider client={new QueryClient()}>
        <Sonda />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  api.marcarLeida.mockResolvedValue({});
});
afterEach(() => vi.unstubAllGlobals());

describe("destino pendiente de un push (arranque en frío)", () => {
  it("reciente: navega a la ruta una sola vez, marca leído y lo borra", async () => {
    const almacen = instalarCaches({ url: "/aulas/solicitudes/42", id: "n-1", en: Date.now() });
    renderizar();
    await waitFor(() =>
      expect(screen.getByTestId("ruta")).toHaveTextContent("/aulas/solicitudes/42"),
    );
    expect(almacen.size).toBe(0);
    expect(api.marcarLeida).toHaveBeenCalledExactlyOnceWith("n-1");

    // Becoming visible again finds nothing left to consume.
    document.dispatchEvent(new Event("visibilitychange"));
    await Promise.resolve();
    expect(api.marcarLeida).toHaveBeenCalledOnce();
  });

  it("vencido o de otro origen: se ignora y se borra, sin navegar", async () => {
    const vencido = instalarCaches({ url: "/tareas/1", id: "n-2", en: Date.now() - 3 * 60 * 1000 });
    renderizar();
    await waitFor(() => expect(vencido.size).toBe(0));
    expect(screen.getByTestId("ruta")).toHaveTextContent("/portal");

    vi.unstubAllGlobals();
    const externo = instalarCaches({ url: "//evil.example.com", id: null, en: Date.now() });
    document.dispatchEvent(new Event("visibilitychange"));
    await waitFor(() => expect(externo.size).toBe(0));
    expect(screen.getByTestId("ruta")).toHaveTextContent("/portal");
    expect(api.marcarLeida).not.toHaveBeenCalled();
  });
});
