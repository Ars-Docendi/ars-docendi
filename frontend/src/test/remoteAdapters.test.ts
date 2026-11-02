import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../shared/api/client";
import { crearRol, listarPermisos, reemplazarPermisos } from "../features/roles/api/rolesApi";
import {
  crearPeriodo,
  editarPeriodo,
  eliminarPeriodo,
  listarPeriodos,
} from "../features/designaciones/api/periodosApi";
import {
  crearEducacion,
  crearExperiencia,
  crearProyecto,
  editarProyecto,
  guardarCv,
} from "../shared/perfil/api/portalApi";

vi.mock("../shared/api/client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());

describe("adapters HTTP administrativos", () => {
  it("roles consulta catálogo, copia base y reemplaza membresía sólo al guardar", async () => {
    const rol = {
      id: "r1",
      codigo: "observador",
      nombre: "Observador",
      descripcion: "Lee",
      ambito: "global",
      esSistema: false,
      activo: true,
      version: 4,
      permisos: [],
    };
    vi.mocked(apiClient.post).mockResolvedValue({ data: rol });
    await crearRol({ nombre: "Observador", descripcion: "Lee", scope: "global" }, "base-1");
    expect(apiClient.post).toHaveBeenCalledWith(
      "/api/administracion/roles",
      expect.objectContaining({ rolBaseId: "base-1" }),
    );

    vi.mocked(apiClient.get).mockResolvedValue({
      data: [{ id: "p1", codigo: "usuarios.ver", nombre: "Ver", descripcion: "Consulta" }],
    });
    expect(await listarPermisos()).toHaveLength(1);
    vi.mocked(apiClient.put).mockResolvedValue({ data: [] });
    await reemplazarPermisos({ ...rol, scope: "global", es_sistema: false }, ["p1"]);
    expect(apiClient.put).toHaveBeenCalledWith("/api/administracion/roles/r1/permisos", {
      permisoIds: ["p1"],
      version: 4,
    });
  });

  it("períodos consulta y muta las rutas persistentes", async () => {
    const periodo = {
      id: "p1",
      nombre: "2C",
      cargaDesde: "2026-01-01",
      cargaHasta: "2026-02-01",
      impactoDesde: "2026-03-01",
      impactoHasta: "2026-07-01",
      activo: true,
      version: 1,
    };
    vi.mocked(apiClient.get).mockResolvedValue({ data: [periodo] });
    expect(await listarPeriodos()).toEqual([periodo]);
    vi.mocked(apiClient.post).mockResolvedValue({ data: periodo });
    await crearPeriodo(periodo);
    vi.mocked(apiClient.put).mockResolvedValue({ data: periodo });
    await editarPeriodo("p1", periodo);
    await eliminarPeriodo("p1");
    expect(apiClient.delete).toHaveBeenCalledWith("/api/designaciones/periodos/p1");
  });

  it("portal adapta la metadata del documento de proyectos", async () => {
    const proyecto = {
      nombre: "Proyecto",
      rol: "Directora",
      descripcion: "Descripción",
      desde: "2026",
      hasta: null,
      documento: { nombre: "informe.pdf" },
      doi: "",
    };
    vi.mocked(apiClient.post).mockResolvedValue({ data: proyecto });
    vi.mocked(apiClient.put).mockResolvedValue({ data: proyecto });

    await crearProyecto(proyecto);
    await editarProyecto("p1", proyecto);

    const payload = expect.objectContaining({
      desde: "2026-01-01",
      documentoNombre: "informe.pdf",
      documentoUri: null,
    });
    expect(apiClient.post).toHaveBeenCalledWith("/api/portal/perfil/proyectos", payload);
    expect(apiClient.put).toHaveBeenCalledWith("/api/portal/perfil/proyectos/p1", payload);
  });

  it("portal completa los períodos parciales para el contrato DateOnly", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: {} });

    await crearExperiencia({
      puesto: "Docente",
      organizacion: "UNLaM",
      descripcion: "Descripción",
      desde: "2020",
      hasta: null,
    });
    await crearEducacion({
      nivel: "Grado",
      carrera: "Ingeniería",
      institucion: "UNLaM",
      desde: "2015-03",
      hasta: "2020",
    });

    expect(apiClient.post).toHaveBeenNthCalledWith(
      1,
      "/api/portal/perfil/experiencia",
      expect.objectContaining({ desde: "2020-01-01", hasta: null }),
    );
    expect(apiClient.post).toHaveBeenNthCalledWith(
      2,
      "/api/portal/perfil/educacion",
      expect.objectContaining({ desde: "2015-03-01", hasta: "2020-01-01" }),
    );
  });

  it("portal envía al guardar el CV solamente los campos aceptados por el backend", async () => {
    vi.mocked(apiClient.put).mockResolvedValue({ data: {} });

    await guardarCv({ nombre: "cv.pdf" });

    expect(apiClient.put).toHaveBeenCalledWith("/api/portal/perfil/cv", {
      nombre: "cv.pdf",
      uri: null,
    });
  });
});
