import { beforeEach, describe, expect, it, vi } from "vitest";

import { apiClient } from "../../../shared/api/client";
import { consultarEstadoSistema, listarAuditoria } from "./sistemaApi";

vi.mock("../../../shared/api/client", () => ({
  apiClient: { get: vi.fn() },
}));

const comprobacionDb = {
  estado: "disponible",
  comprobadoEn: "2026-09-23T18:00:00Z",
  duracionMs: 12,
};

beforeEach(() => vi.mocked(apiClient.get).mockReset());

describe("consultarEstadoSistema", () => {
  it("sondea cada módulo y PostgreSQL por separado y conserva resultados parciales", async () => {
    vi.mocked(apiClient.get).mockImplementation(async (url) => {
      const ruta = String(url);
      if (ruta === "/api/tareas/ping") throw new Error("No exponer este detalle");
      if (ruta === "/api/designaciones/ping") return { data: { status: "maintenance" } } as never;
      if (ruta === "/api/administracion/sistema/estado") return { data: comprobacionDb } as never;
      if (ruta === "/api/inferencia/estado") return { data: comprobacionDb } as never;
      return { data: { status: "ok" } } as never;
    });

    const resultado = await consultarEstadoSistema();

    expect(resultado.modulos.map(({ id, estado }) => [id, estado])).toEqual([
      ["aulas", "disponible"],
      ["tareas", "no_disponible"],
      ["designaciones", "desconocido"],
      ["portal", "disponible"],
    ]);
    expect(resultado.baseDatos.estado).toBe("disponible");
    expect(JSON.stringify(resultado)).not.toContain("No exponer este detalle");
    expect(resultado.inferencia).toMatchObject({ nombre: "Inferencia (IA)", estado: "disponible" });
    expect(apiClient.get).toHaveBeenCalledTimes(6);
  });

  it.each([
    ["deshabilitado", "deshabilitado"],
    ["no_disponible", "no_disponible"],
    ["otro", "desconocido"],
  ])("mapea el estado de inferencia %s como %s", async (recibido, esperado) => {
    vi.mocked(apiClient.get).mockImplementation(async (url) =>
      String(url) === "/api/inferencia/estado"
        ? ({ data: { ...comprobacionDb, estado: recibido } } as never)
        : ({ data: comprobacionDb } as never),
    );

    const resultado = await consultarEstadoSistema();

    expect(resultado.inferencia.estado).toBe(esperado);
  });

  it("informa la inferencia como no disponible ante una falla de red sin ocultar el resto", async () => {
    vi.mocked(apiClient.get).mockImplementation(async (url) => {
      if (String(url) === "/api/inferencia/estado") throw new Error("https://ia.interno");
      if (String(url) === "/api/administracion/sistema/estado")
        return { data: comprobacionDb } as never;
      return { data: { status: "ok" } } as never;
    });

    const resultado = await consultarEstadoSistema();

    expect(resultado.inferencia.estado).toBe("no_disponible");
    expect(resultado.baseDatos.estado).toBe("disponible");
    expect(JSON.stringify(resultado)).not.toContain("ia.interno");
  });
});

describe("listarAuditoria", () => {
  it("envía filtros y página sólo por GET", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { elementos: [] } } as never);
    const filtros = {
      pagina: 2,
      tamanoPagina: 50,
      schema: "identity",
      tabla: "personas",
      accion: "UPDATE",
      actor: "Vidal",
      rowPk: "abc-123",
    };

    await listarAuditoria(filtros);

    expect(apiClient.get).toHaveBeenCalledWith("/api/administracion/auditoria", {
      params: filtros,
    });
  });
});
