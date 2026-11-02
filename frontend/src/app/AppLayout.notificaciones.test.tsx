import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, RouterProvider, createMemoryRouter, useParams } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { notificacion, pagina } from "../features/notificaciones/pruebas";
import { reiniciarGuardias, useGuardiaDeCambios } from "../shared/navegacion/guardias";
import { useResaltado } from "../shared/ui/resaltado";
import AppLayout from "./AppLayout";

const api = vi.hoisted(() => ({
  contarNoLeidas: vi.fn(),
  listarPagina: vi.fn(),
  marcarLeida: vi.fn(),
  marcarLeidas: vi.fn(),
}));
vi.mock("../features/notificaciones/api/notificacionesApi", async (importarOriginal) => ({
  ...(await importarOriginal<typeof import("../features/notificaciones/api/notificacionesApi")>()),
  ...api,
}));

const sesion = vi.hoisted(() => ({ roleCode: "docente" }));
vi.mock("../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      id: "1",
      name: "Vidal, Ernesto",
      initials: "EV",
      upn: "evidal@unlam.edu.ar",
      role: "Docente",
      roleCode: sesion.roleCode,
      permissions: ["portal.ver"],
      asignacion: null,
      asignaciones: [],
      requiereElegirRol: false,
    },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));

function DetalleTarea() {
  const { id } = useParams();
  const resaltada = useResaltado(id);
  return (
    <section
      aria-label="Datos de la tarea"
      data-entidad-id={id}
      className={resaltada ? "adoc-resaltado" : ""}
    >
      Tarea {id}
    </section>
  );
}

function PaginaConCambios() {
  useGuardiaDeCambios(true);
  return <p>Formulario con cambios sin guardar</p>;
}

function renderizar(ruta = "/portal") {
  const router = createMemoryRouter(
    [
      {
        path: "/",
        element: <AppLayout />,
        children: [
          { path: "tareas/:id", element: <DetalleTarea /> },
          { path: "formulario", element: <PaginaConCambios /> },
          { path: "*", element: <Link to="/otra">ir a otra</Link> },
        ],
      },
    ],
    { initialEntries: [ruta] },
  );
  const cliente = new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } });
  const resultado = render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { ...resultado, router, cliente };
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  sesion.roleCode = "docente";
  api.contarNoLeidas.mockResolvedValue(0);
  api.listarPagina.mockResolvedValue(pagina([]));
  api.marcarLeida.mockResolvedValue({});
});

afterEach(() => {
  vi.unstubAllGlobals();
  reiniciarGuardias();
});

describe("AppLayout — campana de notificaciones", () => {
  it("on a narrow screen the bell moves to the top bar", async () => {
    vi.stubGlobal("matchMedia", (consulta: string) => ({
      matches: true,
      media: consulta,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const { container } = renderizar();
    await screen.findByRole("button", { name: /^Notificaciones/ });
    expect(container.querySelector(".adoc-topbar .adoc-campana")).not.toBeNull();
    expect(container.querySelector(".adoc-sidebar-bell")).toBeNull();
    expect(screen.getAllByRole("button", { name: /^Notificaciones/ })).toHaveLength(1);
  });

  it("shows the unread count of the active role and fetches it again when the role changes", async () => {
    api.contarNoLeidas.mockResolvedValueOnce(4).mockResolvedValueOnce(150);
    const usuario = userEvent.setup();
    renderizar();
    expect(
      await screen.findByRole("button", { name: "Notificaciones, 4 notificaciones sin leer" }),
    ).toBeInTheDocument();

    sesion.roleCode = "jefe_catedra";
    await usuario.click(screen.getByRole("link", { name: "ir a otra" })); // any re-render of the shell
    expect(
      await screen.findByRole("button", { name: "Notificaciones, 150 notificaciones sin leer" }),
    ).toBeInTheDocument();
    expect(screen.getByTestId("insignia-pildora")).toHaveTextContent("99+");
    expect(api.contarNoLeidas).toHaveBeenCalledTimes(2);
  });

  it("opens the panel from the bell and shows the exact empty copy", async () => {
    const usuario = userEvent.setup();
    renderizar();
    const campana = await screen.findByRole("button", { name: "Notificaciones, ninguna sin leer" });
    expect(campana).toHaveAttribute("aria-expanded", "false");

    await usuario.click(campana);

    const dialogo = await screen.findByRole("dialog", { name: "Notificaciones" });
    expect(campana).toHaveAttribute("aria-expanded", "true");
    expect(within(dialogo).getByText("No tenés notificaciones sin leer")).toBeInTheDocument();

    await usuario.click(campana); // the bell toggles
    expect(screen.queryByRole("dialog", { name: "Notificaciones" })).not.toBeInTheDocument();
  });

  it("clicking a notification marks it read, closes the panel, navigates and highlights the entity", async () => {
    const n = notificacion({
      titulo: "Tarea asignada",
      tipoEntidad: "TAREA",
      entidadId: "tarea-77",
    });
    api.contarNoLeidas.mockResolvedValue(1);
    api.listarPagina.mockResolvedValue(pagina([n]));
    const usuario = userEvent.setup();
    const { router } = renderizar();
    await usuario.click(
      await screen.findByRole("button", { name: "Notificaciones, 1 notificación sin leer" }),
    );

    await usuario.click(await screen.findByTestId(`notificacion-${n.id}`));

    await waitFor(() => expect(router.state.location.pathname).toBe("/tareas/tarea-77"));
    expect(api.marcarLeida).toHaveBeenCalledWith(n.id);
    expect(screen.queryByRole("dialog", { name: "Notificaciones" })).not.toBeInTheDocument();
    const tarjeta = await screen.findByRole("region", { name: "Datos de la tarea" });
    expect(tarjeta).toHaveClass("adoc-resaltado");
    // The highlight is brief: it fades by itself.
    await waitFor(() => expect(tarjeta).not.toHaveClass("adoc-resaltado"), { timeout: 4500 });
  }, 10000);

  it("a notification opened over a page with unsaved changes keeps the panel open if the user keeps editing", async () => {
    const n = notificacion({
      titulo: "Tarea asignada",
      tipoEntidad: "TAREA",
      entidadId: "tarea-9",
    });
    api.contarNoLeidas.mockResolvedValue(1);
    api.listarPagina.mockResolvedValue(pagina([n]));
    const usuario = userEvent.setup();
    const { router } = renderizar("/formulario");
    await usuario.click(
      await screen.findByRole("button", { name: "Notificaciones, 1 notificación sin leer" }),
    );

    await usuario.click(await screen.findByTestId(`notificacion-${n.id}`));

    await screen.findByText("Tenés cambios sin guardar.");
    expect(router.state.location.pathname).toBe("/formulario");
    await usuario.click(screen.getByRole("button", { name: "Seguir editando" }));
    expect(screen.getByRole("dialog", { name: "Notificaciones" })).toBeInTheDocument();

    // Discarding the changes lets the navigation through and closes the panel.
    await usuario.click(screen.getByTestId(`notificacion-${n.id}`));
    await usuario.click(await screen.findByRole("button", { name: "Descartar cambios" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/tareas/tarea-9"));
    expect(screen.queryByRole("dialog", { name: "Notificaciones" })).not.toBeInTheDocument();
  });
});
