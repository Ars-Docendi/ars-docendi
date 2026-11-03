import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { notificacion, pagina } from "../pruebas";
import {
  INTERVALO_SONDEO_MS,
  notificacionesKeys,
  useCantidadNoLeidas,
  useListaNotificaciones,
  VIGENCIA_MS,
} from "./useNotificaciones";

const api = vi.hoisted(() => ({
  contarNoLeidas: vi.fn(),
  listarPagina: vi.fn(),
  marcarLeida: vi.fn(),
  marcarLeidas: vi.fn(),
}));
vi.mock("../api/notificacionesApi", async (importarOriginal) => ({
  ...(await importarOriginal<typeof import("../api/notificacionesApi")>()),
  ...api,
}));

function envoltorio() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } });
  const Envoltorio = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={cliente}>{children}</QueryClientProvider>
  );
  return { cliente, Envoltorio };
}

beforeEach(() => vi.clearAllMocks());

describe("claves de consulta", () => {
  it("include the active role in every key", () => {
    expect(notificacionesKeys.cantidad("docente")).toEqual([
      "notificaciones",
      "docente",
      "cantidad",
    ]);
    expect(notificacionesKeys.lista("jefe_catedra", "LEIDAS")).toEqual([
      "notificaciones",
      "jefe_catedra",
      "lista",
      "LEIDAS",
    ]);
    expect(notificacionesKeys.cantidad("docente")).not.toEqual(
      notificacionesKeys.cantidad("jefe_catedra"),
    );
  });
});

describe("useCantidadNoLeidas", () => {
  it("fetches again, and keeps each role's count apart, when the active role changes", async () => {
    api.contarNoLeidas.mockImplementation(async () =>
      api.contarNoLeidas.mock.calls.length === 1 ? 3 : 8,
    );
    const { cliente, Envoltorio } = envoltorio();
    const { result, rerender } = renderHook(({ rol }) => useCantidadNoLeidas(rol), {
      wrapper: Envoltorio,
      initialProps: { rol: "docente" },
    });
    await waitFor(() => expect(result.current.data).toBe(3));

    rerender({ rol: "jefe_catedra" });
    // Another role is another query: it never shows the previous role's number.
    expect(result.current.data).toBeUndefined();
    await waitFor(() => expect(result.current.data).toBe(8));

    expect(api.contarNoLeidas).toHaveBeenCalledTimes(2);
    expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(3);
    expect(cliente.getQueryData(notificacionesKeys.cantidad("jefe_catedra"))).toBe(8);
  });

  it("polls every 60 s and refetches on focus once older than 30 s", async () => {
    api.contarNoLeidas.mockResolvedValue(1);
    const { cliente, Envoltorio } = envoltorio();
    const { result } = renderHook(() => useCantidadNoLeidas("docente"), { wrapper: Envoltorio });
    await waitFor(() => expect(result.current.data).toBe(1));

    const opciones = cliente
      .getQueryCache()
      .find({ queryKey: notificacionesKeys.cantidad("docente") })!.observers[0].options;
    expect(opciones.refetchInterval).toBe(INTERVALO_SONDEO_MS);
    expect(INTERVALO_SONDEO_MS).toBe(60_000);
    expect(opciones.staleTime).toBe(VIGENCIA_MS);
    expect(VIGENCIA_MS).toBe(30_000);
    expect(opciones.refetchOnWindowFocus).toBe(true);
  });
});

describe("useListaNotificaciones", () => {
  it("does not fetch until it is enabled (panel closed or other tab)", async () => {
    api.listarPagina.mockResolvedValue(pagina([notificacion()]));
    const { Envoltorio } = envoltorio();
    const { result, rerender } = renderHook(
      ({ habilitada }) => useListaNotificaciones("docente", "NO_LEIDAS", habilitada),
      { wrapper: Envoltorio, initialProps: { habilitada: false } },
    );
    await new Promise((r) => setTimeout(r, 30));
    expect(api.listarPagina).not.toHaveBeenCalled();

    rerender({ habilitada: true });
    await waitFor(() => expect(result.current.data?.pages).toHaveLength(1));
    expect(api.listarPagina).toHaveBeenCalledWith("NO_LEIDAS", 1);
  });

  it("asks for the next page number only while there are more pages", async () => {
    api.listarPagina.mockImplementation(async (_e: string, numero: number) =>
      pagina([notificacion()], { pagina: numero, totalPaginas: 2, total: 21 }),
    );
    const { Envoltorio } = envoltorio();
    const { result } = renderHook(() => useListaNotificaciones("docente", "NO_LEIDAS", true), {
      wrapper: Envoltorio,
    });
    await waitFor(() => expect(result.current.hasNextPage).toBe(true));
    await result.current.fetchNextPage();
    await waitFor(() => expect(result.current.data?.pages).toHaveLength(2));
    expect(api.listarPagina).toHaveBeenLastCalledWith("NO_LEIDAS", 2);
    expect(result.current.hasNextPage).toBe(false);
  });
});
