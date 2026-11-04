import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";

import { sesion } from "../testing/sesionPrueba";
import { BuscadorAyuda } from "./BuscadorAyuda";

vi.mock("../../../shared/auth/useCurrentUser", () => import("../testing/sesionPrueba"));
vi.mock("../contenido", () => import("../testing/contenidoPrueba"));

function Ubicacion() {
  const { pathname, search } = useLocation();
  return <output aria-label="ubicación">{pathname + search}</output>;
}

function renderBuscador() {
  render(
    <MemoryRouter initialEntries={["/ayuda"]}>
      <Routes>
        <Route
          path="*"
          element={
            <>
              <BuscadorAyuda />
              <Ubicacion />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
  return screen.getByRole("combobox", { name: "Buscar en el centro de ayuda" });
}

beforeEach(() => {
  sesion.permisos = ["tareas.ver", "tareas.gestionar", "designaciones.revisar", "roles.ver"];
});

describe("BuscadorAyuda", () => {
  it("arranca cerrado con los atributos ARIA del combobox", () => {
    const entrada = renderBuscador();

    expect(entrada).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Borrar búsqueda" })).not.toBeInTheDocument();
  });

  it("al escribir muestra resultados con título y módulo", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "tareas");

    expect(entrada).toHaveAttribute("aria-expanded", "true");
    const opciones = within(screen.getByRole("listbox")).getAllByRole("option");
    expect(opciones[0]).toHaveTextContent("Ver tus tareas");
    expect(opciones[0]).toHaveTextContent("Tareas de prueba");
  });

  it("↓ ↓ Enter abre el segundo resultado", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "tarea");
    const opciones = screen.getAllByRole("option");
    expect(opciones.length).toBeGreaterThanOrEqual(2);
    expect(entrada).not.toHaveAttribute("aria-activedescendant");

    await user.keyboard("{ArrowDown}{ArrowDown}");
    expect(entrada).toHaveAttribute("aria-activedescendant", opciones[1].id);
    expect(opciones[1]).toHaveAttribute("aria-selected", "true");

    await user.keyboard("{Enter}");
    expect(screen.getByLabelText("ubicación")).toHaveTextContent("/ayuda/tareas/crear-tarea");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("↑ desde el primero vuelve al último", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();
    await user.type(entrada, "tarea");
    const opciones = screen.getAllByRole("option");

    await user.keyboard("{ArrowDown}{ArrowUp}");

    expect(entrada).toHaveAttribute("aria-activedescendant", opciones[opciones.length - 1].id);
  });

  it("Enter sin opción activa abre la página de resultados", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "pedidos  nuevos");
    await user.keyboard("{Enter}");

    expect(screen.getByLabelText("ubicación")).toHaveTextContent(
      "/ayuda/buscar?q=pedidos%20%20nuevos",
    );
  });

  it("con más de cinco coincidencias muestra cinco y 'Ver todos los resultados'", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "introduccion");

    expect(screen.getAllByRole("option")).toHaveLength(5);
    const todos = screen.getByRole("link", { name: "Ver todos los resultados" });
    expect(todos).toHaveAttribute("href", "/ayuda/buscar?q=introduccion");
  });

  it("presionar dentro del panel no le quita el foco al campo", async () => {
    // Safari no enfoca un enlace al hacer clic: sin esto, el blur cierra el panel antes del click.
    const user = userEvent.setup();
    const entrada = renderBuscador();
    await user.type(entrada, "introduccion");

    const todos = screen.getByRole("link", { name: "Ver todos los resultados" });
    expect(fireEvent.mouseDown(todos)).toBe(false);
  });

  it("con cinco o menos coincidencias no ofrece 'Ver todos los resultados'", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "tablero");

    expect(screen.getAllByRole("option")).toHaveLength(1);
    expect(
      screen.queryByRole("link", { name: "Ver todos los resultados" }),
    ).not.toBeInTheDocument();
  });

  it("sin coincidencias avisa con la consulta", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "zzzz");

    expect(screen.getByText("Sin resultados para «zzzz»")).toBeInTheDocument();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("no revela artículos que la persona no puede ver", async () => {
    sesion.permisos = ["tareas.ver"];
    const user = userEvent.setup();
    const entrada = renderBuscador();

    await user.type(entrada, "granulares");

    expect(screen.getByText("Sin resultados para «granulares»")).toBeInTheDocument();
  });

  it("Esc cierra el panel y el botón de limpiar vacía el campo", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();
    await user.type(entrada, "tareas");

    await user.keyboard("{Escape}");
    expect(entrada).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(entrada).toHaveValue("tareas");

    await user.click(screen.getByRole("button", { name: "Borrar búsqueda" }));
    expect(entrada).toHaveValue("");
    expect(entrada).toHaveFocus();
    expect(screen.queryByRole("button", { name: "Borrar búsqueda" })).not.toBeInTheDocument();
  });

  it("al hacer clic en un resultado navega al artículo y cierra el panel", async () => {
    const user = userEvent.setup();
    const entrada = renderBuscador();
    await user.type(entrada, "tablero");

    await user.click(screen.getByRole("option", { name: /Revisar el tablero/ }));

    expect(screen.getByLabelText("ubicación")).toHaveTextContent(
      "/ayuda/designaciones/revisar-tablero",
    );
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });
});
