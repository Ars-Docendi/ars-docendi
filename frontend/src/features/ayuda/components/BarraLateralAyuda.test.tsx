import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { prepararEntornoDom } from "../testing/entornoDom";
import { renderAyuda } from "../testing/renderAyuda";
import { sesion } from "../testing/sesionPrueba";

vi.mock("../../../shared/auth/useCurrentUser", () => import("../testing/sesionPrueba"));
vi.mock("../contenido", () => import("../testing/contenidoPrueba"));

beforeEach(() => {
  prepararEntornoDom();
  sesion.permisos = ["tareas.ver", "tareas.gestionar", "roles.ver"];
});

async function barra() {
  return within(await screen.findByRole("navigation", { name: "Documentación de ayuda" }));
}

describe("BarraLateralAyuda", () => {
  it("muestra título, buscador y los módulos visibles", async () => {
    renderAyuda("/ayuda");
    const lateral = await barra();

    expect(lateral.getByRole("link", { name: "Centro de ayuda" })).toHaveAttribute(
      "href",
      "/ayuda",
    );
    expect(lateral.getByRole("combobox", { name: "Buscar en el centro de ayuda" })).toBeVisible();
    const modulos = lateral.getAllByRole("button", { expanded: false });
    expect(modulos.map((b) => b.textContent)).toEqual([
      "General de prueba",
      "Tareas de prueba",
      "Administración de prueba",
    ]);
    expect(lateral.queryByText("Designaciones de prueba")).not.toBeInTheDocument();
  });

  it("en el inicio todos los módulos están colapsados y se expanden a demanda", async () => {
    const user = userEvent.setup();
    renderAyuda("/ayuda");
    const lateral = await barra();
    expect(lateral.queryByRole("link", { name: "Ver tus tareas" })).not.toBeInTheDocument();

    await user.click(lateral.getByRole("button", { name: "Tareas de prueba" }));

    expect(lateral.getByRole("button", { name: "Tareas de prueba" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    expect(lateral.getByRole("link", { name: "Ver tus tareas" })).toHaveAttribute(
      "href",
      "/ayuda/tareas/ver-tareas",
    );

    await user.click(lateral.getByRole("button", { name: "Tareas de prueba" }));
    expect(lateral.queryByRole("link", { name: "Ver tus tareas" })).not.toBeInTheDocument();
  });

  it("expande el módulo del artículo activo y lista sus pasos bajo él", async () => {
    renderAyuda("/ayuda/general/bienvenida");
    const lateral = await barra();

    expect(await lateral.findByRole("button", { name: "General de prueba" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    expect(lateral.getByRole("button", { name: "Tareas de prueba" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    const actual = lateral.getByRole("link", { name: "Bienvenida al sistema" });
    expect(actual).toHaveAttribute("aria-current", "page");
    expect(lateral.getByRole("link", { name: "Atajos de teclado" })).not.toHaveAttribute(
      "aria-current",
    );

    const pasos = within(lateral.getByRole("list", { name: "En este artículo" })).getAllByRole(
      "link",
    );
    expect(pasos.map((p) => p.textContent)).toEqual([
      "Ingreso",
      "Recorré el menú",
      "Cerrá la sesión",
    ]);
    expect(pasos.map((p) => p.getAttribute("href"))).toEqual([
      "/ayuda/general/bienvenida#paso-1",
      "/ayuda/general/bienvenida#paso-2",
      "/ayuda/general/bienvenida#paso-3",
    ]);
  });

  it("un artículo de un solo paso no muestra lista de pasos", async () => {
    renderAyuda("/ayuda/general/atajos");
    const lateral = await barra();

    await lateral.findByRole("link", { name: "Atajos de teclado" });
    expect(lateral.queryByRole("list", { name: "En este artículo" })).not.toBeInTheDocument();
  });

  it("al navegar a otro módulo lo expande", async () => {
    const user = userEvent.setup();
    renderAyuda("/ayuda/general/bienvenida");
    const lateral = await barra();
    await user.click(await lateral.findByRole("button", { name: "Tareas de prueba" }));

    await user.click(lateral.getByRole("link", { name: "Crear una tarea" }));

    expect(lateral.getByRole("button", { name: "Tareas de prueba" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    expect(lateral.getByRole("link", { name: "Crear una tarea" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("solo lista artículos visibles", async () => {
    sesion.permisos = ["tareas.ver"];
    const user = userEvent.setup();
    renderAyuda("/ayuda/tareas/ver-tareas");
    const lateral = await barra();

    expect(await lateral.findByRole("link", { name: "Ver tus tareas" })).toBeVisible();
    expect(lateral.queryByRole("link", { name: "Crear una tarea" })).not.toBeInTheDocument();
    expect(lateral.queryByRole("button", { name: "Administración de prueba" })).toBeNull();
    await user.click(lateral.getByRole("button", { name: "General de prueba" }));
    expect(lateral.getByRole("link", { name: "Bienvenida al sistema" })).toBeVisible();
  });

  describe("buscador y ?q=", () => {
    const nombre = "Buscar en el centro de ayuda";

    it("la carga directa de /ayuda/buscar?q= refleja la consulta en el campo", async () => {
      renderAyuda("/ayuda/buscar?q=xyz");
      const lateral = await barra();

      expect(lateral.getByRole("combobox", { name: nombre })).toHaveValue("xyz");
    });

    it("fuera de la búsqueda el campo arranca vacío aunque haya ?q=", async () => {
      renderAyuda("/ayuda?q=xyz");
      const lateral = await barra();

      expect(lateral.getByRole("combobox", { name: nombre })).toHaveValue("");
    });

    it("al elegir un resultado desde la búsqueda el panel se cierra y el campo se reinicia", async () => {
      const user = userEvent.setup();
      renderAyuda("/ayuda/buscar?q=tarea");
      const lateral = await barra();
      const campo = lateral.getByRole("combobox", { name: nombre });

      await user.click(campo);
      await user.click(within(await screen.findByRole("listbox")).getAllByRole("option")[0]);

      expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
      // El campo se reinicia cuando se renderiza la ruta nueva, que llega en una transición.
      await waitFor(() => expect(campo).toHaveValue(""));
    });

    it("al elegir un resultado tecleado fuera de la búsqueda el texto queda y el panel se cierra", async () => {
      const user = userEvent.setup();
      renderAyuda("/ayuda");
      const lateral = await barra();
      const campo = lateral.getByRole("combobox", { name: nombre });

      await user.type(campo, "tarea");
      await user.click(within(await screen.findByRole("listbox")).getAllByRole("option")[0]);

      expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
      expect(campo).toHaveValue("tarea");
    });
  });
});
