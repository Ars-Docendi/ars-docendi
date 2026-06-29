import { describe, it, expect } from "vitest";
import { act, render, screen } from "@testing-library/react";
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

describe("TableroRevisionPage — vista Tabla / Tablero", () => {
  it("abre en la vista Tablero con el board completo (vista 'completa') por default", async () => {
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    renderPage();

    // Default = Tablero (Kanban): la columna "En revisión" existe como región.
    expect(await screen.findByRole("region", { name: "En revisión" })).toBeInTheDocument();
    // La vista Tabla NO está montada (su encabezado "Asignatura" no aparece).
    expect(screen.queryByText("Asignatura")).not.toBeInTheDocument();
    // El filtro de turno abre en "Vista completa" (board lleno; "mis-pendientes" oculta
    // los terminales porque no son "tu turno").
    expect(screen.getByLabelText("Filtrar pedidos por turno")).toHaveValue("completa");
    // El switch marca "Tablero" como vista activa.
    expect(screen.getByRole("button", { name: "Tablero" })).toHaveAttribute("aria-pressed", "true");
  });

  it("al elegir 'Tabla' muestra la vista tabular sobre los mismos pedidos del ámbito", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    renderPage();

    await screen.findByRole("region", { name: "En revisión" });
    await user.click(screen.getByRole("button", { name: "Tabla" }));

    // La Tabla queda montada (su encabezado "Asignatura" aparece)...
    expect(screen.getByText("Asignatura")).toBeInTheDocument();
    // ...y las columnas del Kanban (regiones) ya no.
    expect(screen.queryByRole("region", { name: "En revisión" })).not.toBeInTheDocument();
  });
});
