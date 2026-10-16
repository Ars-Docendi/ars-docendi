import { beforeEach, describe, expect, it, vi } from "vitest";
import { AxiosError } from "axios";

import { apiClient } from "../../../shared/api/client";
import { comprobarPing, consultarEstadoSistema, COMPONENTES_PING } from "./sistemaApi";

vi.mock("../../../shared/api/client", () => ({
  apiClient: { get: vi.fn() },
}));

beforeEach(() => vi.mocked(apiClient.get).mockReset());

const ASISTENTE = COMPONENTES_PING.find((c) => c.id === "asistente")!;

describe("comprobarPing", () => {
  it("disponible cuando el ping responde status ok", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { status: "ok" } } as never);

    const resultado = await comprobarPing(ASISTENTE);

    expect(resultado).toMatchObject({ id: "asistente", disponible: true });
    expect(resultado.motivoFalla).toBeUndefined();
  });

  it("distingue timeout de error genérico, sin exponer el detalle interno", async () => {
    vi.mocked(apiClient.get).mockRejectedValueOnce(
      new AxiosError("timeout of 5000ms exceeded", "ECONNABORTED"),
    );
    const timeout = await comprobarPing(ASISTENTE);
    expect(timeout.motivoFalla).toBe("timeout");

    vi.mocked(apiClient.get).mockRejectedValueOnce(new Error("credenciales inválidas: secreto=x"));
    const error = await comprobarPing(ASISTENTE);
    expect(error.motivoFalla).toBe("error");
    expect(JSON.stringify(error)).not.toContain("secreto");
  });

  it("no ok sin ser 'ok' cuenta como no disponible con motivo error", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { status: "maintenance" } } as never);
    const resultado = await comprobarPing(ASISTENTE);
    expect(resultado.disponible).toBe(false);
    expect(resultado.motivoFalla).toBe("error");
  });
});

describe("consultarEstadoSistema", () => {
  it("normaliza estado y mantenimiento, y falla a desconocido/no_disponible sin detalle interno", async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        estado: "disponible",
        comprobadoEn: "2026-09-23T18:00:00Z",
        duracionMs: 12,
        mantenimientoAsistente: "activo",
      },
    } as never);
    const ok = await consultarEstadoSistema();
    expect(ok).toEqual({
      estado: "disponible",
      comprobadoEn: "2026-09-23T18:00:00Z",
      duracionMs: 12,
      mantenimientoAsistente: "activo",
    });

    vi.mocked(apiClient.get).mockRejectedValueOnce(new Error("connection string oculta"));
    const falla = await consultarEstadoSistema();
    expect(falla.estado).toBe("no_disponible");
    expect(falla.mantenimientoAsistente).toBe("desconocido");
    expect(JSON.stringify(falla)).not.toContain("connection string");
  });
});
