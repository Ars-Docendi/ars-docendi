import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { observadores, prepararEntornoDom } from "../testing/entornoDom";
import { contenidoAyuda, renderAyuda } from "../testing/renderAyuda";
import { sesion } from "../testing/sesionPrueba";

vi.mock("../../../shared/auth/useCurrentUser", () => import("../testing/sesionPrueba"));
vi.mock("../contenido", () => import("../testing/contenidoPrueba"));

let scrollIntoView: ReturnType<typeof prepararEntornoDom>["scrollIntoView"];

beforeEach(() => {
  ({ scrollIntoView } = prepararEntornoDom());
  sesion.permisos = ["tareas.gestionar", "roles.ver"];
});

function elementosEnfocados() {
  return scrollIntoView.mock.contexts.map((el) => (el as HTMLElement).id);
}

describe("ArticuloAyudaPage", () => {
  it("muestra migas de tres niveles, h1, introducción y pasos numerados con nota", async () => {
    renderAyuda("/ayuda/general/bienvenida");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Bienvenida al sistema",
    );
    const migas = contenido.getByRole("navigation", { name: "Migas de pan" });
    expect(within(migas).getByRole("link", { name: "General de prueba" })).toHaveAttribute(
      "href",
      "/ayuda/general",
    );
    expect(within(migas).getByText("Bienvenida al sistema")).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(contenido.getByText("Introducción de la bienvenida.")).toBeInTheDocument();

    for (const numero of [1, 2, 3]) {
      expect(document.getElementById(`paso-${numero}`)).toBeInTheDocument();
    }
    const paso2 = within(document.getElementById("paso-2")!);
    expect(paso2.getByRole("heading", { name: /Recorré el menú/ })).toBeInTheDocument();
    expect(paso2.getByRole("note")).toHaveTextContent("Atención");
    expect(paso2.getByRole("note")).toHaveTextContent("Texto de la nota importante.");
    expect(contenido.queryByText("Pretitle")).not.toBeInTheDocument();
  });

  it("muestra la captura solo en el paso que la declara", async () => {
    renderAyuda("/ayuda/general/bienvenida");
    const contenido = await contenidoAyuda();
    await contenido.findByRole("heading", { level: 1 });

    expect(
      within(document.getElementById("paso-1")!).getByRole("img", { name: "Pantalla de ingreso" }),
    ).toBeInTheDocument();
    expect(within(document.getElementById("paso-2")!).queryByRole("img")).not.toBeInTheDocument();
    expect(document.querySelectorAll(".ayuda-captura")).toHaveLength(1);
  });

  it("muestra 'Qué sucede después', preguntas frecuentes y relacionados visibles", async () => {
    renderAyuda("/ayuda/general/bienvenida");
    const contenido = await contenidoAyuda();

    expect(await contenido.findByRole("heading", { name: "¿Qué sucede después?" })).toBeVisible();
    expect(contenido.getByText("Ya podés trabajar.")).toBeInTheDocument();
    const faq = document.getElementById("preguntas-frecuentes")!;
    expect(within(faq).getByText("¿Puedo cambiar de cuenta?")).toBeInTheDocument();

    const relacionados = within(
      contenido.getByRole("heading", { name: "Artículos relacionados" }).parentElement!,
    );
    expect(relacionados.getAllByRole("link").map((a) => a.getAttribute("href"))).toEqual([
      "/ayuda/tareas/crear-tarea",
      "/ayuda/administracion/gestionar-roles",
    ]);
  });

  it("no lista relacionados que la persona no puede ver ni deja títulos vacíos", async () => {
    sesion.permisos = ["tareas.gestionar"];
    renderAyuda("/ayuda/general/bienvenida");
    const contenido = await contenidoAyuda();

    await contenido.findByRole("heading", { level: 1 });
    const relacionados = within(
      contenido.getByRole("heading", { name: "Artículos relacionados" }).parentElement!,
    );
    expect(relacionados.getAllByRole("link")).toHaveLength(1);
    expect(relacionados.queryByText("Administrar roles")).not.toBeInTheDocument();
  });

  it("sin preguntas, sin 'después' y sin relacionados no muestra esas secciones", async () => {
    renderAyuda("/ayuda/general/atajos");
    const contenido = await contenidoAyuda();

    await contenido.findByRole("heading", { level: 1, name: "Atajos de teclado" });
    expect(contenido.queryByRole("heading", { name: "Preguntas frecuentes" })).toBeNull();
    expect(contenido.queryByRole("heading", { name: "Artículos relacionados" })).toBeNull();
    expect(contenido.queryByRole("heading", { name: "¿Qué sucede después?" })).toBeNull();
    expect(document.getElementById("preguntas-frecuentes")).toBeNull();
  });

  it.each([
    ["slug de artículo desconocido", "/ayuda/general/no-existe"],
    ["artículo de otro módulo", "/ayuda/tareas/gestionar-roles"],
    ["artículo sin permiso", "/ayuda/designaciones/gestionar-periodos"],
  ])("redirige al inicio de la ayuda: %s", async (_caso, ruta) => {
    const router = renderAyuda(ruta);

    const contenido = await contenidoAyuda();
    expect(await contenido.findByRole("heading", { level: 1 })).toHaveTextContent(
      "Centro de ayuda",
    );
    expect(router.state.location.pathname).toBe("/ayuda");
    expect(screen.queryByText("Períodos de designación")).not.toBeInTheDocument();
  });

  describe("deep links", () => {
    it("#paso-N posiciona la vista en el paso", async () => {
      renderAyuda("/ayuda/general/bienvenida#paso-2");
      await contenidoAyuda();

      await waitFor(() => expect(elementosEnfocados()).toContain("paso-2"));
    });

    it("#preguntas-frecuentes posiciona la vista en la sección", async () => {
      renderAyuda("/ayuda/general/bienvenida#preguntas-frecuentes");
      await contenidoAyuda();

      await waitFor(() => expect(elementosEnfocados()).toContain("preguntas-frecuentes"));
    });

    it("sin hash abre el artículo arriba del contenedor con scroll, con las migas visibles", async () => {
      const router = renderAyuda("/ayuda/general/bienvenida");
      await contenidoAyuda();
      const principal = document.querySelector<HTMLElement>(".adoc-main")!;

      principal.scrollTop = 32;
      await act(() => router.navigate("/ayuda/tareas/crear-tarea"));

      await waitFor(() => expect(principal.scrollTop).toBe(0));
      expect(scrollIntoView).not.toHaveBeenCalled();
    });
  });

  describe("pasos en la sidebar de documentación", () => {
    it("el clic desplaza suavemente hasta el paso y deja #paso-N en la URL", async () => {
      const user = userEvent.setup();
      const router = renderAyuda("/ayuda/general/bienvenida");
      const lista = await screen.findByRole("list", { name: "En este artículo" });
      scrollIntoView.mockClear();

      await user.click(within(lista).getByRole("link", { name: "Cerrá la sesión" }));

      expect(router.state.location.hash).toBe("#paso-3");
      await waitFor(() => expect(elementosEnfocados()).toContain("paso-3"));
      expect(scrollIntoView).toHaveBeenLastCalledWith({ behavior: "smooth", block: "start" });
    });

    it("respeta prefers-reduced-motion", async () => {
      vi.stubGlobal("matchMedia", (consulta: string) => ({
        matches: consulta.includes("reduce"),
        media: consulta,
        addEventListener: () => {},
        removeEventListener: () => {},
      }));
      const user = userEvent.setup();
      renderAyuda("/ayuda/general/bienvenida");
      const lista = await screen.findByRole("list", { name: "En este artículo" });
      scrollIntoView.mockClear();

      await user.click(within(lista).getByRole("link", { name: "Cerrá la sesión" }));

      await waitFor(() =>
        expect(scrollIntoView).toHaveBeenLastCalledWith({ behavior: "auto", block: "start" }),
      );
      vi.unstubAllGlobals();
    });

    it("resalta el paso que pasa a ser visible", async () => {
      renderAyuda("/ayuda/general/bienvenida");
      const lista = await screen.findByRole("list", { name: "En este artículo" });
      await waitFor(() => expect(observadores.length).toBeGreaterThan(0));
      expect(within(lista).getByRole("link", { name: "Ingreso" })).toHaveAttribute(
        "aria-current",
        "location",
      );

      act(() => observadores.at(-1)!.interseccion(document.getElementById("paso-2")!));

      expect(within(lista).getByRole("link", { name: "Recorré el menú" })).toHaveAttribute(
        "aria-current",
        "location",
      );
      expect(within(lista).getByRole("link", { name: "Ingreso" })).not.toHaveAttribute(
        "aria-current",
      );
    });
  });
});
