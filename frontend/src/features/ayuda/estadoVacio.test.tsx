import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";

import { prepararEntornoDom } from "./testing/entornoDom";
import { contenidoAyuda, renderAyuda } from "./testing/renderAyuda";
import { sesion } from "./testing/sesionPrueba";

// Contenido con módulos pero sin ningún artículo: toda la ayuda debe degradar sin romperse.
vi.mock("../../shared/auth/useCurrentUser", () => import("./testing/sesionPrueba"));
vi.mock("./contenido", async () => {
  const { MODULOS } = await import("./testing/contenidoPrueba");
  return { MODULOS, ARTICULOS: [], CONTENIDO: { modulos: MODULOS, articulos: [] } };
});

beforeEach(() => {
  prepararEntornoDom();
  sesion.permisos = ["tareas.ver", "tareas.gestionar", "roles.ver"];
});

describe("Centro de ayuda sin artículos visibles", () => {
  it("el inicio avisa que no hay ayuda disponible, sin filas ni listas vacías", async () => {
    renderAyuda("/ayuda");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByText(/Todavía no hay artículos de ayuda/)).toBeInTheDocument();
    expect(contenido.queryByRole("list")).not.toBeInTheDocument();
  });

  it("la sidebar de documentación no muestra módulos vacíos y el buscador sigue operativo", async () => {
    renderAyuda("/ayuda");

    const lateral = await screen.findByRole("navigation", { name: "Documentación de ayuda" });
    expect(lateral.querySelectorAll("button[aria-expanded]")).toHaveLength(0);
    expect(screen.getByRole("combobox", { name: "Buscar en el centro de ayuda" })).toBeVisible();
  });

  it("un módulo o un artículo por URL redirige al inicio", async () => {
    const router = renderAyuda("/ayuda/tareas");
    await contenidoAyuda();
    expect(router.state.location.pathname).toBe("/ayuda");

    await router.navigate("/ayuda/tareas/x");
    await waitFor(() => expect(router.state.location.pathname).toBe("/ayuda"));
  });

  it("la búsqueda informa que no hay resultados", async () => {
    renderAyuda("/ayuda/buscar?q=tareas");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByText(/No encontramos resultados/)).toBeInTheDocument();
  });
});
