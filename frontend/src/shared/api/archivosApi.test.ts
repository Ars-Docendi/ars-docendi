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

  it("declara el MIME por la extensión aunque el sistema no informe el tipo", async () => {
    vi.mocked(apiClient.post)
      .mockResolvedValueOnce({
        data: { archivoId: "a-2", urlSubida: "/api/archivos/cargas/a-2/objeto", expiraEn: "" },
      })
      .mockResolvedValueOnce({
        data: { id: "a-2", nombreOriginal: "baja.PDF", estado: "disponible" },
      });
    vi.mocked(apiClient.put).mockResolvedValue({ data: undefined });

    // Windows puede entregar un PDF con `type` vacío; el backend lo rechazaría por MIME.
    await subirArchivo("justificativo", new File(["%PDF-1.7"], "baja.PDF", { type: "" }));

    expect(vi.mocked(apiClient.post).mock.calls[0]?.[1]).toMatchObject({
      mimeDeclarado: "application/pdf",
    });
    expect(vi.mocked(apiClient.put).mock.calls[0]?.[2]).toMatchObject({
      headers: { "Content-Type": "application/pdf" },
    });
  });

  it("no inicia la carga de un formato que el propósito no admite", async () => {
    await expect(
      subirArchivo("dni_frente", new File(["x"], "dni.pdf", { type: "application/pdf" })),
    ).rejects.toThrow("JPG o PNG");
    expect(apiClient.post).not.toHaveBeenCalled();
  });
});
