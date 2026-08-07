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
  it("abre directamente en la Tabla, sin switcher de vistas", async () => {
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    renderPage();

    // La Tabla queda montada (su encabezado "Asignatura" aparece)...
    expect(await screen.findByText("Asignatura")).toBeInTheDocument();
    // ...no hay switcher Tablero/Tabla ni columnas Kanban.
    expect(screen.queryByRole("button", { name: "Tablero" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Tabla" })).not.toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "En revisión" })).not.toBeInTheDocument();
  });

  it("el filtro de turno abre en 'Vista completa' por default", async () => {
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    renderPage();

    await screen.findByText("Asignatura");
    expect(screen.getByLabelText("Filtrar pedidos por turno")).toHaveValue("completa");
  });

  describe("filtro estilo Mis Pedidos (mismo componente FiltrosLista)", () => {
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

    it("agregar y quitar el filtro opcional Legajo acota la lista", async () => {
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
      expect(screen.queryByLabelText("Filtrar por legajo")).not.toBeInTheDocument();
    });
  });
});
