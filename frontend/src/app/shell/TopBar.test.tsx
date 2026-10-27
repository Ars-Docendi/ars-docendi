import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import { TopBar } from "./TopBar";
import type { CurrentUser } from "../../shared/auth/useCurrentUser";

const usuario: CurrentUser = {
  id: "00000000-0000-4000-8000-000000000001",
  name: "Vidal, Ernesto",
  initials: "EV",
  upn: "sistemas@unlam.edu.ar",
  role: "Docente",
  roleCode: "docente",
  permissions: [],
};

function renderTopBar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter>
        <TopBar collapsed={false} onToggleCollapse={() => {}} user={usuario} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("TopBar", () => {
  it("mantiene el menú de usuario con Cerrar sesión", async () => {
    renderTopBar();

    await userEvent.click(screen.getByRole("button", { name: "Abrir menú de usuario" }));

    expect(screen.getByRole("button", { name: "Cerrar sesión" })).toBeInTheDocument();
  });
});
