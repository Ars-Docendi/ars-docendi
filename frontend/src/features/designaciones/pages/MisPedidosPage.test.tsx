import { describe, it, expect } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { MisPedidosPage } from "./MisPedidosPage";

// Integración: monta la página con el stack real de hooks → api mock → store,
// usando el usuario stub (Jefe de Cátedra). Verifica Loading → Success y que
// la precarga del seed se muestre acotada a la cátedra del JC.
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
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });

    // Un pedido en borrador ofrece "Enviar"; uno ya enviado no.
    expect(screen.getAllByRole("button", { name: /Enviar a revisión/ }).length).toBeGreaterThan(0);
    expect(screen.getByText("Valeria Suárez")).toBeInTheDocument();
  });
});
