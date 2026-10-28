import { beforeEach, describe, expect, it, vi } from "vitest";
import { within } from "@testing-library/react";

import { prepararEntornoDom } from "../testing/entornoDom";
import { contenidoAyuda, renderAyuda } from "../testing/renderAyuda";
import { sesion } from "../testing/sesionPrueba";

vi.mock("../../../shared/auth/useCurrentUser", () => import("../testing/sesionPrueba"));
vi.mock("../contenido", () => import("../testing/contenidoPrueba"));

beforeEach(() => {
  prepararEntornoDom();
  sesion.permisos = ["designaciones.revisar", "periodos.administrar"];
});

describe("ResultadosAyudaPage", () => {
  it("lista los artículos que coinciden con módulo, resumen y cantidad", async () => {
    renderAyuda("/ayuda/buscar?q=periodos");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Resultados de la búsqueda",
    );
    expect(contenido.getByText(/1 resultado para «periodos»/)).toBeInTheDocument();
    const fila = contenido.getByRole("link", { name: /Períodos de designación/ });
    expect(fila).toHaveAttribute("href", "/ayuda/designaciones/gestionar-periodos");
    expect(fila).toHaveTextContent("Designaciones de prueba");
    expect(fila).toHaveTextContent("Alta y edición de períodos.");
  });

  it("pluraliza la cantidad", async () => {
    renderAyuda("/ayuda/buscar?q=introduccion");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByText(/\d+ resultados para «introduccion»/)).toBeInTheDocument();
  });

  it("migas: Inicio, Centro de ayuda y Resultados", async () => {
    renderAyuda("/ayuda/buscar?q=periodos");
    const contenido = await contenidoAyuda();

    const migas = await contenido.findByRole("navigation", { name: "Migas de pan" });
    expect(within(migas).getByText("Resultados")).toHaveAttribute("aria-current", "page");
    expect(within(migas).getByRole("link", { name: "Centro de ayuda" })).toBeInTheDocument();
  });

  it("sin coincidencias muestra el estado vacío con enlace al inicio de la ayuda", async () => {
    renderAyuda("/ayuda/buscar?q=zzzz");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByText(/No encontramos resultados para «zzzz»/)).toBeInTheDocument();
    expect(contenido.getByRole("link", { name: "Volver al inicio de la ayuda" })).toHaveAttribute(
      "href",
      "/ayuda",
    );
  });

  it("sin consulta muestra el estado vacío", async () => {
    renderAyuda("/ayuda/buscar");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByText(/Escribí qué querés buscar/)).toBeInTheDocument();
    expect(
      contenido.getByRole("link", { name: "Volver al inicio de la ayuda" }),
    ).toBeInTheDocument();
  });

  it("no revela ni cuenta artículos que la persona no puede ver", async () => {
    renderAyuda("/ayuda/buscar?q=granulares");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByText(/No encontramos resultados/)).toBeInTheDocument();
    expect(contenido.queryByText("Administrar roles")).not.toBeInTheDocument();
  });

  it("`buscar` no se interpreta como módulo", async () => {
    const router = renderAyuda("/ayuda/buscar?q=periodos");
    await contenidoAyuda();

    expect(router.state.location.pathname).toBe("/ayuda/buscar");
  });
});
