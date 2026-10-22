import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, within } from "@testing-library/react";

import { prepararEntornoDom } from "../testing/entornoDom";
import { contenidoAyuda, renderAyuda } from "../testing/renderAyuda";
import { sesion } from "../testing/sesionPrueba";

vi.mock("../../../shared/auth/useCurrentUser", () => import("../testing/sesionPrueba"));
vi.mock("../contenido", () => import("../testing/contenidoPrueba"));

beforeEach(() => {
  prepararEntornoDom();
  sesion.permisos = [];
});

describe("InicioAyudaPage", () => {
  it("lista solo los módulos visibles, con título y descripción", async () => {
    sesion.permisos = ["tareas.ver", "roles.ver"];
    renderAyuda("/ayuda");
    const contenido = await contenidoAyuda();

    const filas = within(await contenido.findByRole("list")).getAllByRole("link");
    expect(filas).toHaveLength(3);
    expect(filas[0]).toHaveTextContent("General de prueba");
    expect(filas[0]).toHaveTextContent("Módulo abierto a todos.");
    expect(filas[0]).toHaveAttribute("href", "/ayuda/general");
    expect(filas[1]).toHaveAttribute("href", "/ayuda/tareas");
    expect(filas[2]).toHaveAttribute("href", "/ayuda/administracion");
    expect(contenido.queryByText("Designaciones de prueba")).not.toBeInTheDocument();
  });

  it("un usuario sin permisos de módulos ve igualmente los módulos abiertos", async () => {
    renderAyuda("/ayuda");
    const contenido = await contenidoAyuda();

    expect(await contenido.findAllByRole("link", { name: /General de prueba/ })).toHaveLength(1);
    expect(contenido.queryByText("Tareas de prueba")).not.toBeInTheDocument();
  });

  it("sigue la convención de encabezado: migas desde Inicio y título sin pretitle", async () => {
    renderAyuda("/ayuda");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      /^Centro de ayuda$/,
    );
    const migas = contenido.getByRole("navigation", { name: "Migas de pan" });
    expect(within(migas).getByRole("link", { name: "Inicio" })).toHaveAttribute("href", "/");
    expect(within(migas).getByText("Centro de ayuda")).toHaveAttribute("aria-current", "page");
  });

  it("muestra también la sidebar de documentación", async () => {
    renderAyuda("/ayuda");

    expect(await screen.findByRole("navigation", { name: "Documentación de ayuda" })).toBeVisible();
  });
});
