import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../shared/api/client";
import { esIntegridadComprometida, exportarLote } from "./loteApi";

vi.mock("../../../shared/api/client", () => ({
  apiClient: { get: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());

describe("loteApi HTTP", () => {
  it("descarga el lote como Blob del período solicitado", async () => {
    const archivo = new Blob(["xlsx"]);
    vi.mocked(apiClient.get).mockResolvedValue({ data: archivo });

    await expect(exportarLote("periodo-1")).resolves.toBe(archivo);
    expect(apiClient.get).toHaveBeenCalledWith("/api/designaciones/periodos/periodo-1/lote.xlsx", {
      responseType: "blob",
    });
  });

  it("distingue el bloqueo por integridad de otros conflictos del lote", async () => {
    const conflicto = (type: string) =>
      new AxiosError("409", "ERR_BAD_REQUEST", undefined, undefined, {
        status: 409,
        statusText: "Conflict",
        headers: {},
        config: { headers: new AxiosHeaders() },
        data: new Blob([JSON.stringify({ type })], { type: "application/problem+json" }),
      });

    await expect(
      esIntegridadComprometida(
        conflicto("https://ars-docendi/errores/lote-integridad-comprometida"),
      ),
    ).resolves.toBe(true);
    await expect(
      esIntegridadComprometida(conflicto("https://ars-docendi/errores/periodo-inactive")),
    ).resolves.toBe(false);
  });
});
