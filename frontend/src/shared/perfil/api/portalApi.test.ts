import { beforeEach, describe, expect, it, vi } from "vitest";
import { eliminarExperiencia, guardarCv, reemplazarTags } from "./portalApi";
import { apiClient } from "../../../shared/api/client";

vi.mock("../../../shared/api/client", () => ({
  apiClient: {
    put: vi.fn().mockResolvedValue({ data: {} }),
    delete: vi.fn().mockResolvedValue({}),
  },
}));
vi.mock("../../../shared/api/archivosApi", () => ({ subirArchivo: vi.fn() }));

describe("versiones observadas de secciones Portal", () => {
  beforeEach(() => vi.clearAllMocks());
  it("envía la versión del ítem al eliminar", async () => {
    await eliminarExperiencia("experiencia", 42);
    expect(apiClient.delete).toHaveBeenCalledWith("/api/portal/perfil/experiencia/experiencia", {
      params: { version: 42 },
    });
  });
  it("conserva la versión del CV leído antes de subir", async () => {
    await guardarCv({ nombre: "legacy.pdf" }, 31);
    expect(apiClient.put).toHaveBeenCalledWith("/api/portal/perfil/cv", {
      nombre: "legacy.pdf",
      uri: null,
      version: 31,
    });
  });
  it("habilidades e intereses envían su propio token", async () => {
    await reemplazarTags("habilidades", ["C#"], 7);
    await reemplazarTags("intereses", ["Docencia"], 3);
    expect(apiClient.put).toHaveBeenNthCalledWith(1, "/api/portal/perfil/habilidades", {
      terminos: ["C#"],
      version: 7,
    });
    expect(apiClient.put).toHaveBeenNthCalledWith(2, "/api/portal/perfil/intereses", {
      terminos: ["Docencia"],
      version: 3,
    });
  });
});
