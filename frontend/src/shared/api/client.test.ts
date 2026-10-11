import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AxiosError, AxiosHeaders, type AxiosAdapter } from "axios";

let apiClient: typeof import("./client").apiClient;

const adaptador: AxiosAdapter = async (config) => ({
  config,
  data: {},
  status: 200,
  statusText: "OK",
  headers: new AxiosHeaders({ "X-XSRF-TOKEN": "token-de-prueba" }),
});

const sinToken: AxiosAdapter = async (config) => ({
  ...(await adaptador(config)),
  headers: new AxiosHeaders(),
});

describe("cliente de la API", () => {
  beforeEach(async () => {
    vi.resetModules();
    ({ apiClient } = await import("./client"));
  });

  afterEach(() => {
    document.cookie = "XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
    vi.restoreAllMocks();
  });

  it("devuelve el token del header sin leer cookies en las mutaciones al mismo origen", async () => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    const respuesta = await apiClient.post("/api/auth/logout", undefined, { adapter: adaptador });

    expect(respuesta.config.headers.get("X-XSRF-TOKEN")).toBe("token-de-prueba");
  });

  it.each([
    { url: "https://externo.test/api/datos" },
    { url: "//externo.test/api/datos" },
    { url: "/api/datos", baseURL: "https://externo.test" },
  ])("no filtra el token a otro origen ($url, $baseURL)", async (destino) => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    const respuesta = await apiClient.request({
      ...destino,
      method: "post",
      adapter: adaptador,
      withXSRFToken: true,
      headers: { "X-XSRF-TOKEN": "token-de-prueba" },
    });

    expect(respuesta.config.headers.has("X-XSRF-TOKEN")).toBe(false);
  });

  it("ignora tokens recibidos de otro origen", async () => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    await apiClient.get("https://externo.test/api/sesion", {
      adapter: async (config) => ({
        ...(await adaptador(config)),
        headers: new AxiosHeaders({ "x-xsrf-token": "token-ajeno" }),
      }),
    });
    const respuesta = await apiClient.post("/api/datos", {}, { adapter: adaptador });
    expect(respuesta.config.headers.get("X-XSRF-TOKEN")).toBe("token-de-prueba");
  });

  it("olvida el token después de cerrar sesión", async () => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    await apiClient.post("/api/auth/logout?origen=menu", undefined, { adapter: sinToken });
    const respuesta = await apiClient.post("/api/datos", {}, { adapter: sinToken });
    expect(respuesta.config.headers.has("X-XSRF-TOKEN")).toBe(false);
  });

  it("olvida el token cuando la sesión responde 401", async () => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    await expect(
      apiClient.get("/api/auth/sesion", {
        adapter: async (config) => {
          throw new AxiosError("Sin sesión", "ERR_BAD_REQUEST", config, undefined, {
            ...(await sinToken(config)),
            status: 401,
          });
        },
      }),
    ).rejects.toThrow("Sin sesión");
    const respuesta = await apiClient.post("/api/datos", {}, { adapter: sinToken });
    expect(respuesta.config.headers.has("X-XSRF-TOKEN")).toBe(false);
  });

  it.each(["post", "put", "patch", "delete"])("envía el token en %s", async (method) => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    const respuesta = await apiClient.request({ url: "/api/datos", method, adapter: sinToken });
    expect(respuesta.config.headers.get("X-XSRF-TOKEN")).toBe("token-de-prueba");
  });

  it.each(["get", "head", "options"])("no envía el token en %s", async (method) => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    const respuesta = await apiClient.request({ url: "/api/datos", method, adapter: sinToken });
    expect(respuesta.config.headers.has("X-XSRF-TOKEN")).toBe(false);
  });

  it("recupera el token tras recargar y lo renueva con otra consulta de sesión", async () => {
    const antes = await apiClient.post("/api/datos", {}, { adapter: sinToken });
    expect(antes.config.headers.has("X-XSRF-TOKEN")).toBe(false);
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    await apiClient.get("/api/auth/sesion", {
      adapter: async (config) => ({
        ...(await sinToken(config)),
        headers: new AxiosHeaders({ "x-xsrf-token": "token-renovado" }),
      }),
    });
    const despues = await apiClient.post("/api/datos", {}, { adapter: sinToken });
    expect(despues.config.headers.get("X-XSRF-TOKEN")).toBe("token-renovado");
  });

  it("el adaptador XHR envía el token del header e ignora una cookie antigua", async () => {
    document.cookie = "XSRF-TOKEN=token-antiguo; path=/";
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    const encabezados = vi.spyOn(XMLHttpRequest.prototype, "setRequestHeader");
    vi.spyOn(XMLHttpRequest.prototype, "send").mockImplementation(() => undefined);
    const controlador = new AbortController();
    const solicitud = apiClient.post(
      "/api/datos",
      {},
      {
        adapter: "xhr",
        signal: controlador.signal,
        xsrfCookieName: "XSRF-TOKEN",
        withXSRFToken: true,
      },
    );
    const cancelacion = expect(solicitud).rejects.toThrow();
    await vi.waitFor(() =>
      expect(encabezados).toHaveBeenCalledWith("X-XSRF-TOKEN", "token-de-prueba"),
    );
    expect(encabezados).not.toHaveBeenCalledWith("X-XSRF-TOKEN", "token-antiguo");
    controlador.abort();
    await cancelacion;
  });

  it("no recupera un token de una consulta iniciada antes del logout", async () => {
    await apiClient.get("/api/auth/sesion", { adapter: adaptador });
    let completar!: () => void;
    const consulta = apiClient.get("/api/auth/sesion", {
      adapter: async (config) => {
        await new Promise<void>((resolve) => {
          completar = resolve;
        });
        return adaptador(config);
      },
    });
    await vi.waitFor(() => expect(completar).toBeTypeOf("function"));
    await apiClient.post("/api/auth/logout", undefined, { adapter: sinToken });
    completar();
    await consulta;
    const respuesta = await apiClient.post("/api/datos", {}, { adapter: sinToken });
    expect(respuesta.config.headers.has("X-XSRF-TOKEN")).toBe(false);
  });
});
