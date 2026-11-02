import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "./client";
import { subirArchivo } from "./archivosApi";

vi.mock("./client", () => ({
  apiClient: { post: vi.fn(), put: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());

describe("subirArchivo", () => {
  it("no devuelve un archivo rechazado como si estuviera disponible", async () => {
    vi.mocked(apiClient.post)
      .mockResolvedValueOnce({
        data: {
          archivoId: "archivo-1",
          urlSubida: "/api/archivos/cargas/archivo-1/objeto",
          expiraEn: "2026-09-18T18:00:00Z",
        },
      })
      .mockResolvedValueOnce({
        data: {
          id: "archivo-1",
          proposito: "cv",
          nombreOriginal: "cv.pdf",
          mimeDeclarado: "application/pdf",
          mimeDetectado: "application/pdf",
          tamanoBytes: 9,
          sha256: null,
          estado: "rechazado",
        },
      });
    vi.mocked(apiClient.put).mockResolvedValue({ data: undefined });

    await expect(
      subirArchivo("cv", new File(["%PDF-1.7"], "cv.pdf", { type: "application/pdf" })),
    ).rejects.toThrow("rechazado");
    expect(apiClient.post).toHaveBeenCalledTimes(2);
  });
});
