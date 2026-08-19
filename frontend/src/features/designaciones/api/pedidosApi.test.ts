import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../shared/api/client";
import {
  aceptarPedido,
  crearPedido,
  despriorizarPedido,
  devolverPedido,
  eliminarPedido,
  listarPedidosPorAmbito,
  priorizarPedido,
  rechazarPedido,
  reenviarPedido,
} from "./pedidosApi";

vi.mock("../../../shared/api/client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

const dto = {
  id: "pedido-1",
  numero: "2026-0001",
  periodo: { id: "periodo-1", nombre: "2C 2026" },
  persona: { id: "persona-1", nombre: "Ana", apellido: "Pérez", documento: "123", legajo: "7" },
  materia: {
    id: "materia-1",
    codigo: "03500",
    nombre: "Software",
    carreraId: "carrera-1",
    carreraNombre: "Informática",
  },
  novedad: "Alta",
  estado: "en_revision_coordinador",
  prioritario: false,
  cargoSolicitado: { id: "cargo-1", codigo: "adjunto", nombre: "Profesor Adjunto" },
  dedicacionSolicitada: "Categoría 2",
  horas: 10,
  horasInvestigacion: 0,
  horasExternas: 0,
  justificacion: null,
  tipoBaja: null,
  tipoBajaDetalle: null,
  etapaRetorno: null,
  propietarioActual: "coordinador_carrera",
  snapshot: null,
  version: 2,
  adjuntos: [],
  historial: [],
  accionesPermitidas: ["aceptar", "rechazar"],
};
const catalogos = {
  periodoActivo: {
    id: "periodo-1",
    nombre: "2C",
    cargaDesde: "2026-01-01",
    cargaHasta: "2026-02-01",
    impactoDesde: "2026-03-01",
    impactoHasta: "2026-07-01",
    activo: true,
  },
  periodos: [],
  materias: [{ id: "materia-1", codigo: "03500", nombre: "Software", carreraId: "carrera-1" }],
  personas: [
    {
      id: "persona-1",
      nombre: "Ana",
      apellido: "Pérez",
      documento: "123",
      legajo: "7",
      designacionesVigentes: [],
    },
  ],
  cargos: [
    {
      id: "cargo-1",
      codigo: "adjunto",
      nombre: "Profesor Adjunto",
      abreviatura: "Adjunto",
      orden: 3,
    },
  ],
  dedicaciones: ["Categoría 2"],
  tiposBaja: ["Renuncia"],
  novedades: ["Alta"],
};

beforeEach(() => vi.clearAllMocks());

describe("pedidosApi HTTP", () => {
  it("mapea listado y conserva las acciones autorizadas por backend", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: [dto] });
    const pedidos = await listarPedidosPorAmbito();
    expect(apiClient.get).toHaveBeenCalledWith("/api/designaciones/pedidos");
    expect(pedidos[0]).toMatchObject({
      catedra: "Software",
      carrera: "Informática",
      accionesPermitidas: ["aceptar", "rechazar"],
    });
  });

  it("crea con IDs canónicos resueltos desde catálogos HTTP", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: dto });
    await crearPedido(
      {
        docente: { dni: "123", nombre: "Ana", antiguedad: 0 },
        catedra: "Software",
        horas: 10,
        cargoActual: null,
        dedicacionActual: null,
        novedad: "Alta",
        cargoSolicitado: "Profesor Adjunto",
        dedicacionSolicitada: "Categoría 2",
        horasExternas: 0,
        horasInvestigacion: 0,
        adjuntos: [],
      },
      catalogos,
    );
    expect(apiClient.post).toHaveBeenCalledWith(
      "/api/designaciones/pedidos",
      expect.objectContaining({
        periodoId: "periodo-1",
        personaId: "persona-1",
        materiaId: "materia-1",
        cargoSolicitadoId: "cargo-1",
      }),
    );
  });

  it("envía una clave UUID en cada transición", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: dto });
    await aceptarPedido("pedido-1", "Conforme");
    expect(apiClient.post).toHaveBeenCalledWith(
      "/api/designaciones/pedidos/pedido-1/aceptar",
      { comentario: "Conforme" },
      { headers: { "Idempotency-Key": expect.stringMatching(/^[0-9a-f-]{36}$/) } },
    );
  });

  it("usa HTTP para devolución, reenvío, rechazo, prioridad y eliminación", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: dto });
    await devolverPedido("pedido-1", "Corregir");
    await reenviarPedido("pedido-1");
    await rechazarPedido("pedido-1", "No corresponde");
    await priorizarPedido("pedido-1", "Urgente");
    await despriorizarPedido("pedido-1");
    await eliminarPedido("pedido-1");
    for (const accion of ["devolver", "reenviar", "rechazar", "priorizar", "despriorizar"]) {
      expect(apiClient.post).toHaveBeenCalledWith(
        `/api/designaciones/pedidos/pedido-1/${accion}`,
        expect.anything(),
        expect.objectContaining({
          headers: expect.objectContaining({ "Idempotency-Key": expect.any(String) }),
        }),
      );
    }
    expect(apiClient.delete).toHaveBeenCalledWith("/api/designaciones/pedidos/pedido-1");
  });

  it("propaga un conflicto HTTP sin inventar un estado local", async () => {
    const conflicto = Object.assign(new Error("concurrency-conflict"), {
      response: { status: 409 },
    });
    vi.mocked(apiClient.post).mockRejectedValue(conflicto);
    await expect(aceptarPedido("pedido-1")).rejects.toBe(conflicto);
  });
});
