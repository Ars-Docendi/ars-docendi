import { describe, it, expect } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { MisPedidosPage } from "./MisPedidosPage";

// Integración: monta la página con el stack real de hooks → api mock → store,
// usando el usuario stub (Jefe de Cátedra). Verifica Loading → Success, la
// precarga del seed acotada a la cátedra del JC, y el menú kebab de acciones.
function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <MisPedidosPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("MisPedidosPage (integración)", () => {
  it("muestra Loading y luego los pedidos del seed del Jefe de Cátedra", async () => {
    renderPage();

    expect(screen.getByText("Cargando tus pedidos…")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Prof. Laura Giménez")).toBeInTheDocument();
    });
    expect(screen.getByText("Prof. Valeria Suárez")).toBeInTheDocument();
  });

  it("el kebab de un borrador ofrece 'Enviar a revisión'", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Prof. Laura Giménez")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "Acciones del pedido de Laura Giménez" }));
    expect(screen.getByRole("menuitem", { name: /Enviar a revisión/ })).toBeInTheDocument();
  });
});
