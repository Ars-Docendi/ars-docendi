import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RouterProvider, createMemoryRouter } from "react-router-dom";

import { RequireAuth } from "../../shared/auth/RequireAuth";
import { routes } from "./routes";
import { prepararEntornoDom } from "./testing/entornoDom";
import { contenidoAyuda, renderAyuda } from "./testing/renderAyuda";
import { sesion } from "./testing/sesionPrueba";

const autenticacion = vi.hoisted(() => ({ anonimo: false }));

vi.mock("../../shared/auth/useCurrentUser", async () => {
  const base = await import("./testing/sesionPrueba");
  return {
    useCurrentUser: () =>
      autenticacion.anonimo
        ? { user: null, isLoading: false, error: null, retry: () => {} }
        : base.useCurrentUser(),
  };
});
vi.mock("./contenido", () => import("./testing/contenidoPrueba"));

beforeEach(() => {
  prepararEntornoDom();
  autenticacion.anonimo = false;
  sesion.permisos = [];
});

describe("rutas de ayuda", () => {
  it("un anónimo que abre /ayuda va a /login, como cualquier ruta protegida", async () => {
    autenticacion.anonimo = true;
    const router = createMemoryRouter(
      [
        { path: "/login", element: <p>Pantalla de login</p> },
        { element: <RequireAuth />, children: [{ path: "/", children: [routes] }] },
      ],
      { initialEntries: ["/ayuda"] },
    );
    render(
      <QueryClientProvider client={new QueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );

    expect(await screen.findByText("Pantalla de login")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/login");
  });

  it("no exige ningún permiso: un usuario sin permisos de módulos entra", async () => {
    const router = renderAyuda("/ayuda");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Centro de ayuda",
    );
    expect(router.state.location.pathname).toBe("/ayuda");
  });

  it("recorre inicio, módulo, artículo y paso con el router", async () => {
    sesion.permisos = ["tareas.ver"];
    const user = userEvent.setup();
    const router = renderAyuda("/ayuda");
    const contenido = await contenidoAyuda();

    await user.click(await contenido.findByRole("link", { name: /General de prueba/ }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/ayuda/general"));

    await user.click(await contenido.findByRole("link", { name: /Bienvenida al sistema/ }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/ayuda/general/bienvenida"));
    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Bienvenida al sistema",
    );

    await user.click(await screen.findByRole("link", { name: "Cerrá la sesión" }));
    await waitFor(() => expect(router.state.location.hash).toBe("#paso-3"));
  });

  describe("permisos", () => {
    beforeEach(() => {
      sesion.permisos = ["tareas.ver"];
    });

    it("sin roles.ver no se ve el artículo de roles en ninguna superficie", async () => {
      const user = userEvent.setup();
      renderAyuda("/ayuda");
      const lateral = await screen.findByRole("navigation", { name: "Documentación de ayuda" });

      expect(screen.queryByText("Administración de prueba")).not.toBeInTheDocument();
      expect(lateral.textContent).not.toContain("Administrar roles");
      await user.type(
        screen.getByRole("combobox", { name: "Buscar en el centro de ayuda" }),
        "granulares",
      );
      expect(screen.getByText("Sin resultados para «granulares»")).toBeInTheDocument();
    });

    it("la URL directa al artículo de roles redirige al inicio de la ayuda", async () => {
      const router = renderAyuda("/ayuda/administracion/gestionar-roles");
      const contenido = await contenidoAyuda();

      expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
        "Centro de ayuda",
      );
      expect(router.state.location.pathname).toBe("/ayuda");
      expect(screen.queryByText("Administrar roles")).not.toBeInTheDocument();
    });
  });
});
