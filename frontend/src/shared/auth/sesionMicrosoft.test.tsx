import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from "axios";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { SesionApi } from "./sesionMicrosoft";

const banderas = vi.hoisted(() => ({ desarrollo: false }));
const sesionApi = vi.hoisted(() => ({
  obtenerSesion: vi.fn<() => Promise<SesionApi | null>>(),
  cerrarSesion: vi.fn<() => Promise<void>>(),
}));

vi.mock("./developmentAuth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./developmentAuth")>()),
  get developmentAuthEnabled() {
    return banderas.desarrollo;
  },
}));
vi.mock("./sesionMicrosoft", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./sesionMicrosoft")>()),
  microsoftLoginEnabled: true,
  obtenerSesion: sesionApi.obtenerSesion,
  cerrarSesion: sesionApi.cerrarSesion,
}));
vi.mock("./dev/DevLoginModal", () => ({
  DevLoginModal: ({ open }: { open: boolean }) =>
    open ? <div role="dialog">Selector de identidades</div> : null,
}));

const sesion: SesionApi = {
  usuarioId: "u-1",
  nombreParaMostrar: "Ada Lovelace",
  upn: "ada@dominio.edu.ar",
  rolCodigo: "secretaria",
  rolNombre: "Secretaría Académica",
  permisos: ["usuarios.ver"],
};

function Proveedores({ children, ruta }: { children: ReactNode; ruta: string }) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[ruta]}>{children}</MemoryRouter>
    </QueryClientProvider>
  );
}

async function renderizarProtegida() {
  const { RequireAuth } = await import("./RequireAuth");
  const { useCurrentUser } = await import("./useCurrentUser");
  function Contenido() {
    const { user } = useCurrentUser();
    return (
      <p>
        {user?.name} · {user?.role} · {user?.roleCode} · {user?.permissions.join(",")}
      </p>
    );
  }
  render(
    <Proveedores ruta="/portal">
      <Routes>
        <Route path="/login" element={<p>Pantalla de login</p>} />
        <Route element={<RequireAuth />}>
          <Route path="/portal" element={<Contenido />} />
        </Route>
      </Routes>
    </Proveedores>,
  );
}

async function renderizarLogin() {
  const { LoginPage } = await import("./LoginPage");
  render(
    <Proveedores ruta="/login">
      <LoginPage />
    </Proveedores>,
  );
}

describe("sesión por cookie del ingreso con Microsoft", () => {
  const asignar = vi.fn();
  const ubicacionOriginal = window.location;

  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
    banderas.desarrollo = false;
    sesionApi.obtenerSesion.mockReset();
    sesionApi.cerrarSesion.mockReset().mockResolvedValue();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { ...ubicacionOriginal, assign: asignar },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", { configurable: true, value: ubicacionOriginal });
    asignar.mockReset();
  });

  it("muestra la carga mientras se consulta la sesión", async () => {
    sesionApi.obtenerSesion.mockReturnValue(new Promise(() => undefined));

    await renderizarProtegida();

    expect(await screen.findByRole("status")).toHaveTextContent("Cargando sesión");
  });

  it("vuelve al login cuando la API responde que no hay sesión", async () => {
    sesionApi.obtenerSesion.mockResolvedValue(null);

    await renderizarProtegida();

    expect(await screen.findByText("Pantalla de login")).toBeInTheDocument();
  });

  it("construye el usuario con el rol y los permisos de la sesión", async () => {
    sesionApi.obtenerSesion.mockResolvedValue(sesion);

    await renderizarProtegida();

    expect(
      await screen.findByText("Ada Lovelace · Secretaría Académica · secretaria · usuarios.ver"),
    ).toBeInTheDocument();
  });

  it("vuelve al login cuando otra consulta recibe 401", async () => {
    sesionApi.obtenerSesion.mockResolvedValue(sesion);
    await renderizarProtegida();
    await screen.findByText(/Ada Lovelace/);
    const { apiClient } = await import("../api/client");

    await expect(
      apiClient.get("/api/designaciones/catalogos", {
        adapter: async (config: InternalAxiosRequestConfig) => {
          const respuesta = { status: 401, config, data: {}, headers: {}, statusText: "" };
          throw new AxiosError("401", "ERR_BAD_REQUEST", config, null, respuesta as AxiosResponse);
        },
      }),
    ).rejects.toThrow();

    expect(await screen.findByText("Pantalla de login")).toBeInTheDocument();
  });

  it("el botón principal inicia el ingreso con Microsoft hacia la ruta de destino", async () => {
    sesionApi.obtenerSesion.mockResolvedValue(null);
    await renderizarLogin();

    await userEvent.click(
      screen.getByRole("button", { name: "Iniciar sesión con cuenta institucional" }),
    );

    expect(asignar).toHaveBeenCalledWith("/api/auth/login?returnUrl=%2Fportal");
    expect(
      screen.queryByRole("button", { name: "Ingresar con una identidad de desarrollo" }),
    ).not.toBeInTheDocument();
  });

  it("ofrece el selector como acceso secundario cuando ambos están habilitados", async () => {
    banderas.desarrollo = true;
    sesionApi.obtenerSesion.mockResolvedValue(null);
    await renderizarLogin();

    await userEvent.click(
      screen.getByRole("button", { name: "Ingresar con una identidad de desarrollo" }),
    );

    expect(await screen.findByRole("dialog")).toHaveTextContent("Selector de identidades");
    expect(asignar).not.toHaveBeenCalled();
  });

  it("cerrar sesión borra la cookie en el backend y la selección de desarrollo", async () => {
    banderas.desarrollo = true;
    const { seleccionarSesionDesarrollo, obtenerSesionDesarrollo } = await import("./dev/session");
    const { useCerrarSesion } = await import("./auth");
    seleccionarSesionDesarrollo("u-1", "secretaria");
    function Salir() {
      const cerrar = useCerrarSesion();
      return (
        <button type="button" onClick={() => void cerrar()}>
          Salir
        </button>
      );
    }
    render(
      <Proveedores ruta="/">
        <Salir />
      </Proveedores>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Salir" }));

    expect(sesionApi.cerrarSesion).toHaveBeenCalledOnce();
    expect(obtenerSesionDesarrollo()).toBeNull();
  });
});
