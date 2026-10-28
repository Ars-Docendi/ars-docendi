import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, RouterProvider, createMemoryRouter } from "react-router-dom";

import AppLayout from "./AppLayout";

vi.mock("../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      id: "1",
      name: "Vidal, Ernesto",
      initials: "EV",
      upn: "evidal@unlam.edu.ar",
      role: "Docente",
      roleCode: "docente",
      permissions: ["portal.ver"],
    },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));

function renderEn(ruta: string) {
  const router = createMemoryRouter(
    [
      {
        path: "/",
        element: <AppLayout />,
        children: [
          {
            path: "*",
            element: (
              <>
                <Link to="/ayuda/tareas">ir a ayuda</Link>
                <Link to="/ayudante">ir a ayudante</Link>
                <Link to="/portal">ir al portal</Link>
              </>
            ),
          },
        ],
      },
    ],
    { initialEntries: [ruta] },
  );
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const { container } = render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return container.querySelector(".adoc-app");
}

describe("AppLayout — contracción por ruta", () => {
  // La preferencia de sidebar colapsada vive en localStorage: cada test arranca sin ella.
  beforeEach(() => localStorage.clear());

  it("fuera de /ayuda el shell queda expandido", () => {
    expect(renderEn("/portal")).not.toHaveClass("collapsed");
    expect(screen.getByText("Mi portal")).toBeInTheDocument();
  });

  it.each(["/ayuda", "/ayuda/tareas", "/ayuda/tareas/crear-una-tarea"])(
    "bajo %s la sidebar principal queda en íconos",
    (ruta) => {
      expect(renderEn(ruta)).toHaveClass("collapsed");
      expect(screen.getByRole("link", { name: "Centro de ayuda" })).toHaveAttribute(
        "title",
        "Centro de ayuda",
      );
    },
  );

  it("entrar a /ayuda contrae y salir restaura el estado del shell", async () => {
    const user = userEvent.setup();
    const app = renderEn("/portal");

    await user.click(screen.getByRole("link", { name: "ir a ayuda" }));
    expect(app).toHaveClass("collapsed");

    await user.click(screen.getByRole("link", { name: "ir al portal" }));
    expect(app).not.toHaveClass("collapsed");
  });

  it("en /ayuda el botón Menú despliega la sidebar sin cambiar la preferencia", async () => {
    const user = userEvent.setup();
    const app = renderEn("/portal");

    await user.click(screen.getByRole("link", { name: "ir a ayuda" }));
    await user.click(screen.getByRole("button", { name: "Expandir menú lateral" }));
    expect(app).not.toHaveClass("collapsed");

    // Salir y volver: la preferencia sigue expandida y /ayuda arranca contraída otra vez.
    await user.click(screen.getByRole("link", { name: "ir al portal" }));
    expect(app).not.toHaveClass("collapsed");
    await user.click(screen.getByRole("link", { name: "ir a ayuda" }));
    expect(app).toHaveClass("collapsed");
  });

  it("una ruta que solo empieza igual que /ayuda no contrae", async () => {
    const user = userEvent.setup();
    const app = renderEn("/portal");

    await user.click(screen.getByRole("link", { name: "ir a ayudante" }));
    expect(app).not.toHaveClass("collapsed");
  });
});
