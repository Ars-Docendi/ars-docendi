import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

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

function renderTopBar(
  user: CurrentUser = usuario,
  ruta = "/",
  { collapsed = false, onToggleCollapse = () => {} } = {},
) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[ruta]}>
        <TopBar collapsed={collapsed} onToggleCollapse={onToggleCollapse} user={user} />
        <Routes>
          <Route path="/aulas" element={<p>Pantalla de aulas</p>} />
          <Route path="/portal" element={<p>Pantalla del portal</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** jsdom no evalúa media queries: simula un viewport angosto. */
function simularPantallaAngosta() {
  vi.stubGlobal(
    "matchMedia",
    (consulta: string) =>
      ({
        matches: consulta === "(max-width: 767px)",
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("TopBar", () => {
  it("mantiene el menú de usuario con Cerrar sesión", async () => {
    renderTopBar();

    await userEvent.click(screen.getByRole("button", { name: "Abrir menú de usuario" }));

    expect(screen.getByRole("button", { name: "Cerrar sesión" })).toBeInTheDocument();
  });

  it("en pantallas angostas navega desde el panel y lo cierra", async () => {
    simularPantallaAngosta();
    renderTopBar({ ...usuario, permissions: ["portal.ver", "aulas.ver"] }, "/aulas");

    await userEvent.click(screen.getByRole("button", { name: "Abrir menú de navegación" }));
    const panel = screen.getByRole("dialog", { name: "Menú" });
    expect(within(panel).getByRole("link", { name: "Centro de ayuda" })).toHaveAttribute(
      "href",
      "/ayuda",
    );
    await userEvent.click(within(panel).getByRole("link", { name: "Mi portal" }));

    expect(screen.getByText("Pantalla del portal")).toBeInTheDocument();
    expect(screen.queryByRole("dialog", { name: "Menú" })).not.toBeInTheDocument();
  });

  it("en escritorio colapsa la barra lateral sin abrir el panel", async () => {
    const onToggleCollapse = vi.fn();
    renderTopBar(usuario, "/", { onToggleCollapse });

    await userEvent.click(screen.getByRole("button", { name: "Colapsar menú lateral" }));

    expect(onToggleCollapse).toHaveBeenCalledOnce();
    expect(screen.queryByRole("dialog", { name: "Menú" })).not.toBeInTheDocument();
  });
});
