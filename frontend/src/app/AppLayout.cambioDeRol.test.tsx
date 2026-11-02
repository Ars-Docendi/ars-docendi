import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { pagina } from "../features/notificaciones/pruebas";
import { reiniciarGuardias, useGuardiaDeCambios } from "../shared/navegacion/guardias";
import type { AsignacionApi, SesionApi } from "../shared/auth/sesionMicrosoft";
import AppLayout from "./AppLayout";

// Cambiar de rol cambia la clave de la sesión: el layout pasa a "Cargando sesión…" y desmonta el
// modal. Estas pruebas montan el layout real (sin mockear la sesión ni la mutación) para que ese
// desmontaje ocurra como en el navegador.
const config = vi.hoisted(() => ({ desarrollo: true }));
vi.mock("../shared/auth/developmentAuth", () => ({
  get developmentAuthEnabled() {
    return config.desarrollo;
  },
}));
vi.mock("../shared/auth/sesionMicrosoft", async (importarOriginal) => ({
  ...(await importarOriginal<typeof import("../shared/auth/sesionMicrosoft")>()),
  get microsoftLoginEnabled() {
    return !config.desarrollo;
  },
  obtenerSesion: vi.fn(),
  elegirAsignacion: vi.fn(),
}));
vi.mock("../shared/auth/dev/identidadesApi", () => ({ obtenerSesionDesarrolloApi: vi.fn() }));
vi.mock("../shared/auth/auth", () => ({ useCerrarSesion: () => vi.fn() }));

const notificaciones = vi.hoisted(() => ({
  contarNoLeidas: vi.fn(),
  listarPagina: vi.fn(),
}));
vi.mock("../features/notificaciones/api/notificacionesApi", async (importarOriginal) => ({
  ...(await importarOriginal<typeof import("../features/notificaciones/api/notificacionesApi")>()),
  ...notificaciones,
}));

import { obtenerSesionDesarrolloApi } from "../shared/auth/dev/identidadesApi";
import { elegirAsignacion, obtenerSesion } from "../shared/auth/sesionMicrosoft";
import { obtenerSesionDesarrollo } from "../shared/auth/dev/session";

const DOCENTE: AsignacionApi = {
  id: "a-docente",
  rolCodigo: "docente",
  rolNombre: "Docente",
  ambitoTipo: "materia",
  ambitoNombre: "Programación I",
};
const ADMIN: AsignacionApi = {
  id: "a-admin",
  rolCodigo: "administrativo",
  rolNombre: "Administrativo",
  ambitoTipo: "global",
  ambitoNombre: null,
};

const sesionCon = (asignacion: AsignacionApi): SesionApi => ({
  usuarioId: "u-1",
  nombreParaMostrar: "López, Carla",
  upn: "carla@unlam.edu.ar",
  rolCodigo: asignacion.rolCodigo,
  rolNombre: asignacion.rolNombre,
  permisos: [],
  asignacion,
  asignaciones: [DOCENTE, ADMIN],
  requiereElegirRol: false,
});

function FormularioConCambios() {
  useGuardiaDeCambios(true);
  return <p>Formulario con cambios sin guardar</p>;
}

function renderizar(ruta = "/aulas") {
  const router = createMemoryRouter(
    [
      {
        path: "/",
        element: <AppLayout />,
        children: [
          { index: true, element: <p>Inicio</p> },
          { path: "aulas", element: <p>Reserva de aulas</p> },
          { path: "formulario", element: <FormularioConCambios /> },
        ],
      },
    ],
    { initialEntries: [ruta] },
  );
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}

async function abrirCambiarRol() {
  await userEvent.click(await screen.findByRole("button", { name: /Menú de usuario/ }));
  await userEvent.click(screen.getByRole("menuitem", { name: "Cambiar rol" }));
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  config.desarrollo = true;
  notificaciones.contarNoLeidas.mockResolvedValue(0);
  notificaciones.listarPagina.mockResolvedValue(pagina([]));
});

afterEach(reiniciarGuardias);

describe("AppLayout — cambiar de rol", () => {
  describe("con el ingreso de desarrollo", () => {
    beforeEach(() => {
      localStorage.setItem("adoc.dev.userId", "u-1");
      localStorage.setItem("adoc.dev.asignacionId", DOCENTE.id);
      // La sesión sale de la asignación guardada en el navegador, como con los headers reales.
      vi.mocked(obtenerSesionDesarrolloApi).mockImplementation(async () =>
        sesionCon(obtenerSesionDesarrollo()?.asignacionId === ADMIN.id ? ADMIN : DOCENTE),
      );
    });

    it("elegir otro rol desde una página con rol lleva al inicio del rol nuevo", async () => {
      const router = renderizar("/aulas");
      await screen.findByText("Reserva de aulas");

      await abrirCambiarRol();
      await userEvent.click(screen.getByText("Administrativo"));

      await waitFor(() => expect(router.state.location.pathname).toBe("/"));
      expect(await screen.findByText("Inicio")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Menú de usuario.*Administrativo/ })).toBeVisible();
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });

    it("con cambios sin guardar confirma antes, y al descartar cambia el rol y navega sin volver a preguntar", async () => {
      const router = renderizar("/formulario");
      await screen.findByText("Formulario con cambios sin guardar");

      await abrirCambiarRol();
      await userEvent.click(screen.getByText("Administrativo"));
      expect(await screen.findByText("Tenés cambios sin guardar.")).toBeInTheDocument();
      expect(obtenerSesionDesarrollo()?.asignacionId).toBe(DOCENTE.id);

      await userEvent.click(screen.getByRole("button", { name: "Descartar cambios" }));

      await waitFor(() => expect(router.state.location.pathname).toBe("/"));
      expect(await screen.findByText("Inicio")).toBeInTheDocument();
      expect(screen.queryByText("Tenés cambios sin guardar.")).not.toBeInTheDocument();
      expect(obtenerSesionDesarrollo()?.asignacionId).toBe(ADMIN.id);
    });

    it("si el cambio falla, el modal muestra el error y no navega", async () => {
      const router = renderizar("/aulas");
      await screen.findByText("Reserva de aulas");
      await abrirCambiarRol();
      const guardar = vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
        throw new Error("sin espacio");
      });

      await userEvent.click(screen.getByText("Administrativo"));

      expect(await screen.findByText("No se pudo cambiar el rol. Intentá de nuevo.")).toBeVisible();
      expect(router.state.location.pathname).toBe("/aulas");
      guardar.mockRestore();
    });
  });

  describe("con el ingreso de Microsoft", () => {
    beforeEach(() => {
      config.desarrollo = false;
      let activa = DOCENTE;
      vi.mocked(obtenerSesion).mockImplementation(async () => sesionCon(activa));
      vi.mocked(elegirAsignacion).mockImplementation(async (id) => {
        activa = id === ADMIN.id ? ADMIN : DOCENTE;
        return sesionCon(activa);
      });
    });

    it("elegir otro rol lleva al inicio del rol nuevo", async () => {
      const router = renderizar("/aulas");
      await screen.findByText("Reserva de aulas");

      await abrirCambiarRol();
      await userEvent.click(screen.getByText("Administrativo"));

      await waitFor(() => expect(router.state.location.pathname).toBe("/"));
      expect(await screen.findByText("Inicio")).toBeInTheDocument();
      expect(elegirAsignacion).toHaveBeenCalledWith(ADMIN.id);
    });

    it("si el cambio falla, el modal muestra el error y no navega", async () => {
      vi.mocked(elegirAsignacion).mockRejectedValue(new Error("boom"));
      const router = renderizar("/aulas");
      await screen.findByText("Reserva de aulas");

      await abrirCambiarRol();
      await userEvent.click(screen.getByText("Administrativo"));

      expect(await screen.findByText("No se pudo cambiar el rol. Intentá de nuevo.")).toBeVisible();
      expect(router.state.location.pathname).toBe("/aulas");
    });
  });
});
