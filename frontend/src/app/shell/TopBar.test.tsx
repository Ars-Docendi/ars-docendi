import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import { TopBar } from "./TopBar";

const usuario = {
  name: "Vidal, Ernesto",
  initials: "EV",
  upn: "sistemas@unlam.edu.ar",
  role: "Docente",
  roleCode: "docente",
  permissions: [],
};

function renderTopBar() {
  render(
    <MemoryRouter>
      <TopBar collapsed={false} onToggleCollapse={() => {}} user={usuario} />
    </MemoryRouter>,
  );
}

describe("TopBar", () => {
  it("no muestra búsqueda global, notificaciones ni ayuda", () => {
    renderTopBar();

    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Notificaciones" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Ayuda" })).not.toBeInTheDocument();
  });

  it("mantiene el menú de usuario con Cerrar sesión", async () => {
    renderTopBar();

    await userEvent.click(screen.getByRole("button", { name: "Abrir menú de usuario" }));

    expect(screen.getByRole("button", { name: "Cerrar sesión" })).toBeInTheDocument();
  });
});
