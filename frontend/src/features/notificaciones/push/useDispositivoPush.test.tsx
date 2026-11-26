import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({ obtenerClavePublicaPush: vi.fn() }));
vi.mock("../api/pushApi", () => api);

const navegador = vi.hoisted(() => ({
  soportaPush: vi.fn(),
  esIos: vi.fn(),
  esAppInstalada: vi.fn(),
  suscripcionActual: vi.fn(),
  activarEnDispositivo: vi.fn(),
  desactivarEnDispositivo: vi.fn(),
}));
vi.mock("./dispositivoPush", () => navegador);

import { useDispositivoPush } from "./useDispositivoPush";

function renderizar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const envoltorio = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={cliente}>{children}</QueryClientProvider>
  );
  return renderHook(() => useDispositivoPush(), { wrapper: envoltorio });
}

function permiso(valor: NotificationPermission) {
  vi.stubGlobal("Notification", { permission: valor });
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
  api.obtenerClavePublicaPush.mockResolvedValue("clave");
  navegador.soportaPush.mockReturnValue(true);
  navegador.esIos.mockReturnValue(false);
  navegador.esAppInstalada.mockReturnValue(false);
  navegador.suscripcionActual.mockResolvedValue(null);
  permiso("default");
});

describe("estado de este dispositivo", () => {
  it.each([
    [
      "servidor sin claves",
      () => api.obtenerClavePublicaPush.mockResolvedValue(null),
      "servidor-apagado",
    ],
    [
      "iPhone en una pestaña de Safari (sin PushManager)",
      () => {
        navegador.soportaPush.mockReturnValue(false);
        navegador.esIos.mockReturnValue(true);
      },
      "ios-sin-instalar",
    ],
    [
      "iPhone ya instalado pero sin Web Push (iOS viejo)",
      () => {
        navegador.soportaPush.mockReturnValue(false);
        navegador.esIos.mockReturnValue(true);
        navegador.esAppInstalada.mockReturnValue(true);
      },
      "sin-soporte",
    ],
    ["navegador sin Web Push", () => navegador.soportaPush.mockReturnValue(false), "sin-soporte"],
    ["permiso bloqueado", () => permiso("denied"), "bloqueado"],
    [
      "suscripción existente con permiso concedido",
      () => {
        permiso("granted");
        navegador.suscripcionActual.mockResolvedValue({});
      },
      "activo",
    ],
    ["sin suscripción", () => permiso("granted"), "desactivado"],
  ])("%s", async (_nombre, preparar, esperado) => {
    preparar();
    const { result } = renderizar();
    await waitFor(() => expect(result.current.estado).toBe(esperado));
  });

  it("si no puede consultar al servidor lo dice, en vez de dejar la pantalla vacía", async () => {
    api.obtenerClavePublicaPush.mockRejectedValue(new Error("red"));
    const { result } = renderizar();
    await waitFor(() => expect(result.current.estado).toBe("error"));
  });
});

describe("activar y desactivar", () => {
  it("activar pasa a activo; si el usuario deniega el permiso queda bloqueado", async () => {
    const { result } = renderizar();
    await waitFor(() => expect(result.current.estado).toBe("desactivado"));

    navegador.activarEnDispositivo.mockResolvedValueOnce("bloqueado");
    await act(() => result.current.activar());
    expect(result.current.estado).toBe("bloqueado");
  });

  it("un fallo al activar deja el estado como estaba y muestra un mensaje", async () => {
    const { result } = renderizar();
    await waitFor(() => expect(result.current.estado).toBe("desactivado"));

    navegador.activarEnDispositivo.mockRejectedValueOnce(new Error("red"));
    await act(() => result.current.activar());
    expect(result.current.estado).toBe("desactivado");
    expect(result.current.error).toMatch(/No pudimos activar/);
  });

  it("desactivar quita el dispositivo y queda desactivado", async () => {
    permiso("granted");
    navegador.suscripcionActual.mockResolvedValue({});
    const { result } = renderizar();
    await waitFor(() => expect(result.current.estado).toBe("activo"));

    navegador.desactivarEnDispositivo.mockResolvedValue(undefined);
    await act(() => result.current.desactivar());
    expect(result.current.estado).toBe("desactivado");
  });
});
