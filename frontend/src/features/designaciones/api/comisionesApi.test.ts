import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../shared/api/client";
import {
  importarComisiones,
  listarComisiones,
  listarPeriodosGuarani,
  listarPeriodosImportados,
} from "./comisionesApi";

vi.mock("../../../shared/api/client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());

describe("comisionesApi HTTP", () => {
  it("consulta períodos importados y comisiones del snapshot local", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: [] });

    await listarPeriodosImportados();
    await listarComisiones("152");

    expect(apiClient.get).toHaveBeenNthCalledWith(1, "/api/designaciones/comisiones/periodos");
    expect(apiClient.get).toHaveBeenNthCalledWith(2, "/api/designaciones/comisiones", {
      params: { periodoLectivoId: "152" },
    });
  });

  it("pide los períodos lectivos a Guaraní por el backend", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: [{ id: "152", nombre: "2do", anio: 2026 }],
    });

    await expect(listarPeriodosGuarani()).resolves.toEqual([
      { id: "152", nombre: "2do", anio: 2026 },
    ]);
    expect(apiClient.get).toHaveBeenCalledWith(
      "/api/designaciones/comisiones/guarani/periodos-lectivos",
    );
  });

  it("importa enviando sólo el período lectivo elegido", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { cantidad: 3 } });

    await expect(importarComisiones("152")).resolves.toEqual({ cantidad: 3 });
    expect(apiClient.post).toHaveBeenCalledWith("/api/designaciones/comisiones/importaciones", {
      periodoLectivoId: "152",
    });
  });
});
