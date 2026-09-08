import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../shared/api/client";
import { exportarLote } from "./loteApi";

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
});
