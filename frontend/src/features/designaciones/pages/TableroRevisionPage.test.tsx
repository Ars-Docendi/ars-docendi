import { describe, it, expect } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { TableroRevisionPage } from "./TableroRevisionPage";
import { setMockUser, setRolActivo } from "../../../shared/auth/dev/mockSession";

// Usa el stack real (mock session + api mock + React Query), como el test de
// integración del flujo: el seed tiene pedidos del ámbito del Coordinador.
const DEMO_ID = "a0000000-0000-4000-8000-000000000007";

function nuevoClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderPage() {
  return render(
    <QueryClientProvider client={nuevoClient()}>
      <MemoryRouter>
        <TableroRevisionPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("TableroRevisionPage — vista Tabla única (tema E)", () => {
  it("abre directamente en la Tabla, con las pestañas por etapa y sin switcher de vistas", async () => {
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    renderPage();

    // La Tabla queda montada (su encabezado "Estado" aparece)...
    expect(await screen.findByRole("columnheader", { name: "Estado" })).toBeInTheDocument();
    // ...con una sola tabla y las 6 pestañas por área del circuito.
    expect(screen.getAllByRole("table")).toHaveLength(1);
    expect(screen.getAllByRole("tab")).toHaveLength(6);
    // No hay switcher Tablero/Tabla ni columnas Kanban.
    expect(screen.queryByRole("button", { name: "Tablero" })).not.toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "En revisión" })).not.toBeInTheDocument();
  });

  it("el filtro de turno ya no es un Select suelto: las pestañas por etapa lo reemplazan", async () => {
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    renderPage();

    await screen.findByRole("columnheader", { name: "Estado" });
    expect(screen.queryByLabelText("Filtrar pedidos por turno")).not.toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /Todos/ })).toBeInTheDocument();
  });

  describe("filtros globales, arriba de las pestañas", () => {
    it("filtrar por Docente acota la lista", async () => {
      const user = userEvent.setup();
      setMockUser(DEMO_ID);
      act(() => setRolActivo("Coordinador"));
      renderPage();

      await waitFor(() => {
        expect(
          screen.getByRole("button", { name: "Ver el pedido de Valeria Suárez" }),
        ).toBeInTheDocument();
      });
      await user.type(screen.getByLabelText("Filtrar por docente"), "valeria");

      expect(
        screen.getByRole("button", { name: "Ver el pedido de Valeria Suárez" }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Ver el pedido de Pablo Herrera" }),
      ).not.toBeInTheDocument();
    });

    it("los filtros opcionales se agregan y se quitan", async () => {
      const user = userEvent.setup();
      setMockUser(DEMO_ID);
      act(() => setRolActivo("Coordinador"));
      renderPage();

      await waitFor(() => {
        expect(
          screen.getByRole("button", { name: "Ver el pedido de Lucía Fernández" }),
        ).toBeInTheDocument();
      });

      await user.selectOptions(screen.getByLabelText("Añadir filtro"), "legajo");
      await user.type(screen.getByLabelText("Filtrar por legajo"), "1001");

      expect(
        screen.getByRole("button", { name: "Ver el pedido de Lucía Fernández" }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Ver el pedido de Valeria Suárez" }),
      ).not.toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Quitar filtro de legajo" }));
      expect(
        screen.getByRole("button", { name: "Ver el pedido de Valeria Suárez" }),
      ).toBeInTheDocument();
    });

    it("el filtro de período es fijo y abre en el período abierto, no en 'todos'", async () => {
      setMockUser(DEMO_ID);
      act(() => setRolActivo("Coordinador"));
      renderPage();

      await waitFor(() => {
        expect(screen.getByLabelText("Filtrar por período")).toBeInTheDocument();
      });

      // Un revisor trabaja sobre el período en curso; los cerrados son ruido. Y como
      // arranca aplicado, el filtro no puede vivir detrás de "+ Añadir filtro".
      const periodo = screen.getByLabelText("Filtrar por período");
      expect(periodo).toHaveValue("1");

      const opciones = Array.from(periodo.querySelectorAll("option"), (o) => o.textContent);
      // El abierto se rotula como tal, y "Todos" queda al final: es la salida, no el default.
      expect(opciones[0]).toBe("1er cuatrimestre 2026 (abierto)");
      expect(opciones.at(-1)).toBe("Todos los períodos");
      expect(opciones.filter((o) => o?.includes("(abierto)"))).toHaveLength(1);
    });

    /** Opciones que ofrece el selector "+ Añadir filtro". */
    function filtrosOfrecidos() {
      return Array.from(
        screen.getByLabelText("Añadir filtro").querySelectorAll("option"),
        (o) => o.textContent,
      );
    }

    it("el filtro Carrera no se le ofrece a quien ve una sola carrera", async () => {
      setMockUser(DEMO_ID);
      act(() => setRolActivo("Coordinador"));
      renderPage();

      await waitFor(() => {
        expect(screen.getByLabelText("Añadir filtro")).toBeInTheDocument();
      });

      // El ámbito de un Coordinador ES una carrera [BR-009]: no acotaría nada.
      expect(filtrosOfrecidos()).not.toContain("Carrera");
    });

    it("Secretaría, que ve todo el departamento, sí tiene el filtro Carrera", async () => {
      setMockUser(DEMO_ID);
      act(() => setRolActivo("Secretaría"));
      renderPage();

      await waitFor(() => {
        expect(screen.getByLabelText("Añadir filtro")).toBeInTheDocument();
      });

      expect(filtrosOfrecidos()).toContain("Carrera");
    });
  });
});
