import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../shared/api/client";
import {
  cambiarEstadoUsuario,
  crearUsuario,
  listarUsuarios,
} from "../features/usuarios/api/usuariosApi";
import { crearRol, listarPermisos, reemplazarPermisos } from "../features/roles/api/rolesApi";
import { crearDocente, listarDocentes } from "../features/docentes/api/docentesApi";
import {
  crearPeriodo,
  editarPeriodo,
  eliminarPeriodo,
  listarPeriodos,
} from "../features/designaciones/api/periodosApi";
import { crearProyecto, editarProyecto } from "../features/portal/api/portalApi";

vi.mock("../shared/api/client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());

describe("adapters HTTP administrativos", () => {
  it("usuarios lista, crea con IDs de rol/ámbito y cambia estado con versión", async () => {
    const dto = {
      id: "u1",
      personaId: "p1",
      nombre: "Ana",
      apellido: "Pérez",
      documento: "1",
      legajo: "7",
      cuil: null,
      fechaNacimiento: null,
      telefono: null,
      upn: "ana@test",
      activo: true,
      version: 3,
      roles: [
        {
          id: "ur1",
          rolId: "r1",
          codigo: "jefe_catedra",
          nombre: "Jefe de Cátedra",
          ambito: "materia",
          materiaId: "m1",
          carreraId: "c1",
        },
      ],
    };
    vi.mocked(apiClient.get).mockResolvedValue({ data: [dto] });
    expect((await listarUsuarios())[0]).toMatchObject({
      id: "u1",
      roles: ["Jefe de Cátedra"],
      version: 3,
    });

    vi.mocked(apiClient.post).mockResolvedValue({ data: dto });
    const catalogos = {
      roles: [{ id: "r1", codigo: "jefe_catedra", nombre: "Jefe de Cátedra", ambito: "materia" }],
      materias: [{ id: "m1", codigo: "1", nombre: "Software" }],
      carreras: [{ id: "c1", codigo: "INF", nombre: "Informática" }],
    };
    const formulario = {
      nombre: "Ana",
      apellido: "Pérez",
      documento: "1",
      legajo: "7",
      cuil: "",
      fecha_nacimiento: "",
      telefono: "",
      upn: "ana@test",
      roles: ["Jefe de Cátedra" as const],
    };
    await crearUsuario(formulario, catalogos);
    expect(apiClient.post).toHaveBeenCalledWith(
      "/api/administracion/usuarios",
      expect.objectContaining({
        roles: [{ rolId: "r1", materiaId: "m1", carreraId: "c1" }],
      }),
    );
    await cambiarEstadoUsuario({ ...formulario, id: "u1", is_active: true, version: 3 }, false);
    expect(apiClient.post).toHaveBeenLastCalledWith("/api/administracion/usuarios/u1/desactivar", {
      version: 3,
    });
  });

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

  it("docentes usa IDs canónicos de materia y cargo", async () => {
    const dto = {
      personaId: "p1",
      nombre: "Ana",
      apellido: "Pérez",
      documento: "1",
      legajo: "7",
      cuil: null,
      fechaNacimiento: null,
      telefono: null,
      upn: "ana@test",
      activo: true,
      version: 2,
      roles: ["docente"],
      asignaciones: [
        {
          id: "d1",
          materiaId: "m1",
          materiaCodigo: "03500",
          materiaNombre: "Software",
          cargoId: "c1",
          cargoNombre: "Adjunto",
          cargoAbreviatura: "Adj.",
          dedicacion: null,
          horas: 10,
        },
      ],
    };
    vi.mocked(apiClient.get).mockResolvedValue({ data: [dto] });
    expect((await listarDocentes())[0].asignaciones[0].materia.codigo).toBe("03500");
    vi.mocked(apiClient.post).mockResolvedValue({ data: dto });
    await crearDocente(
      {
        nombre: "Ana",
        apellido: "Pérez",
        documento: "1",
        legajo: "7",
        cuil: "",
        fecha_nacimiento: "",
        telefono: "",
        upn: "ana@test",
        roles: ["Docente"],
        asignaciones: [
          {
            materia: { id: "m1", codigo: "03500", nombre: "Software" },
            cargo: "Adjunto",
            horas: 10,
          },
        ],
      },
      {
        roles: [{ id: "r1", codigo: "docente", nombre: "Docente" }],
        materias: [{ id: "m1", codigo: "03500", nombre: "Software" }],
        cargos: [{ id: "c1", codigo: "adjunto", nombre: "Adjunto", abreviatura: "Adj." }],
        personasElegibles: [],
      },
    );
    expect(apiClient.post).toHaveBeenCalledWith(
      "/api/administracion/docentes",
      expect.objectContaining({
        designaciones: [expect.objectContaining({ materiaId: "m1", cargoId: "c1" })],
      }),
    );
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
      desde: "2026-01-01",
      hasta: null,
      documento: { nombre: "informe.pdf" },
      doi: "",
    };
    vi.mocked(apiClient.post).mockResolvedValue({ data: proyecto });
    vi.mocked(apiClient.put).mockResolvedValue({ data: proyecto });

    await crearProyecto(proyecto);
    await editarProyecto("p1", proyecto);

    const payload = expect.objectContaining({
      documentoNombre: "informe.pdf",
      documentoUri: null,
    });
    expect(apiClient.post).toHaveBeenCalledWith("/api/portal/perfil/proyectos", payload);
    expect(apiClient.put).toHaveBeenCalledWith("/api/portal/perfil/proyectos/p1", payload);
  });
});
