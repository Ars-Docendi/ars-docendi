import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, within } from "@testing-library/react";

import { prepararEntornoDom } from "../testing/entornoDom";
import { contenidoAyuda, renderAyuda } from "../testing/renderAyuda";
import { sesion } from "../testing/sesionPrueba";

vi.mock("../../../shared/auth/useCurrentUser", () => import("../testing/sesionPrueba"));
vi.mock("../contenido", () => import("../testing/contenidoPrueba"));

beforeEach(() => {
  prepararEntornoDom();
  sesion.permisos = ["tareas.ver", "tareas.gestionar"];
});

describe("ModuloAyudaPage", () => {
  it("muestra migas, título, descripción y artículos agrupados en el orden de los grupos", async () => {
    renderAyuda("/ayuda/tareas");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Tareas de prueba",
    );
    expect(contenido.getByText("Módulo de tareas.")).toBeInTheDocument();
    const migas = contenido.getByRole("navigation", { name: "Migas de pan" });
    expect(within(migas).getByRole("link", { name: "Centro de ayuda" })).toHaveAttribute(
      "href",
      "/ayuda",
    );
    expect(within(migas).getByText("Tareas de prueba")).toHaveAttribute("aria-current", "page");

    const grupos = contenido.getAllByRole("heading", { level: 2 }).map((h) => h.textContent);
    expect(grupos).toEqual(["Seguir", "Gestionar"]);
    const filas = contenido
      .getAllByRole("link", { name: /tarea/i })
      .filter((a) => a.getAttribute("href")?.startsWith("/ayuda/tareas/"));
    expect(filas.map((a) => a.getAttribute("href"))).toEqual([
      "/ayuda/tareas/ver-tareas",
      "/ayuda/tareas/crear-tarea",
    ]);
    expect(filas[0]).toHaveTextContent("Listado de tareas.");
  });

  it("omite los grupos sin artículos visibles", async () => {
    sesion.permisos = ["tareas.ver"];
    renderAyuda("/ayuda/tareas");
    const contenido = await contenidoAyuda();

    await contenido.findByRole("heading", { level: 1 });
    expect(contenido.getAllByRole("heading", { level: 2 }).map((h) => h.textContent)).toEqual([
      "Seguir",
    ]);
  });

  it.each([
    ["inexistente", "/ayuda/no-existe"],
    ["sin permiso", "/ayuda/administracion"],
    ["de otro perfil", "/ayuda/designaciones"],
  ])("redirige al inicio de la ayuda: módulo %s", async (_caso, ruta) => {
    sesion.permisos = ["tareas.ver"];
    const router = renderAyuda(ruta);

    const contenido = await contenidoAyuda();
    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Centro de ayuda",
    );
    expect(router.state.location.pathname).toBe("/ayuda");
    expect(screen.queryByText("Designaciones de prueba")).not.toBeInTheDocument();
  });
});
