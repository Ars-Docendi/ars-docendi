// El JSON lo genera el backend desde una respuesta real de GET /api/designaciones/pedidos/{id}
// (PedidosHttpTests). Si el backend cambia la forma, falla allí; al regenerarlo, falla aquí.
import { describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../shared/api/client";
import contrato from "../../../test/contratos/pedido-detalle.json";
import { obtenerPedido } from "./pedidosApi";

vi.mock("../../../shared/api/client", () => ({ apiClient: { get: vi.fn() } }));

describe("contrato del detalle de pedido", () => {
  it("mapea la respuesta real del backend sin perder datos", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: contrato });

    const pedido = await obtenerPedido(contrato.id);
    const esperado = {
      id: contrato.id,
      numero: contrato.numero,
      periodoId: contrato.periodo.id,
      materiaId: contrato.materia.materiaId,
      carreraId: contrato.materia.carreraId,
      estado: contrato.estado,
      novedad: contrato.novedad,
      horas: contrato.horas,
      cargoSolicitadoId: contrato.cargoSolicitado.id,
      horasActuales: contrato.snapshot.horas,
      accionesPermitidas: contrato.accionesPermitidas,
      version: contrato.version,
      dni: contrato.persona.documento,
      adjuntos: contrato.adjuntos.map((a) => [a.id, a.tipo, a.estadoArchivo]),
      historial: contrato.historial.map((h) => [h.id, h.accion, h.creadoEn]),
    };
    // Un campo renombrado en el backend llega como undefined a ambos lados: se exige valor.
    expect(JSON.stringify(esperado, (_, v: unknown) => v ?? "AUSENTE")).not.toContain("AUSENTE");

    const { dni, adjuntos, historial, ...campos } = esperado;
    expect(pedido).toMatchObject(campos);
    expect(pedido.docente.dni).toBe(dni);
    expect(pedido.adjuntos.map((a) => [a.id, a.tipo, a.estadoArchivo])).toEqual(adjuntos);
    expect(pedido.historial.map((h) => [h.id, h.accion, h.fecha])).toEqual(historial);
  });
});
