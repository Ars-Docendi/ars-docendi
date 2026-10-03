import { beforeEach, describe, expect, it, vi } from "vitest";

import { apiClient } from "../../../shared/api/client";
import { listarAuditoria } from "./auditoriaApi";

vi.mock("../../../shared/api/client", () => ({
  apiClient: { get: vi.fn() },
}));

beforeEach(() => vi.mocked(apiClient.get).mockReset());

describe("listarAuditoria", () => {
  it("envía filtros y página sólo por GET, con rowPk como nombre de parámetro", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: {
        elementos: [],
        pagina: 1,
        tamanoPagina: 50,
        total: 0,
        parcial: false,
        fuentesNoDisponibles: [],
      },
    } as never);
    const filtros = {
      pagina: 2,
      tamanoPagina: 50,
      modulo: "identity",
      tabla: "personas",
      accion: "UPDATE" as const,
      q: "vidal",
      rowPk: "abc-123",
    };

    await listarAuditoria(filtros);

    expect(apiClient.get).toHaveBeenCalledWith("/api/administracion/auditoria", {
      params: filtros,
    });
  });
});
