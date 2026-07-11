import { describe, it, expect } from "vitest";
import { act, render, screen } from "@testing-library/react";
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
});
