import { afterEach, describe, expect, it, vi } from "vitest";

import { apiClient } from "./client";

describe("cliente de la API", () => {
  afterEach(() => {
    document.cookie = "XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
    vi.restoreAllMocks();
  });

  it("devuelve el token anti-falsificación en las mutaciones al mismo origen", async () => {
    document.cookie = "XSRF-TOKEN=token-de-prueba; path=/";
    // axios agrega el header en su adaptador XHR: se espía la solicitud real sin enviarla.
    const encabezados = vi.spyOn(XMLHttpRequest.prototype, "setRequestHeader");
    vi.spyOn(XMLHttpRequest.prototype, "send").mockImplementation(() => undefined);

    void apiClient.post("/api/auth/logout", undefined, { adapter: "xhr" });

    await vi.waitFor(() =>
      expect(encabezados).toHaveBeenCalledWith("X-XSRF-TOKEN", "token-de-prueba"),
    );
  });
});
