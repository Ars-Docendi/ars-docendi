import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import { DetalleSolicitudPage } from "./DetalleSolicitudPage";
import type { SolicitudReservaAula } from "../types";

const api = vi.hoisted(() => ({
  obtenerSolicitud: vi.fn(),
  asignarAula: vi.fn(),
  rechazarSolicitud: vi.fn(),
  cancelarReserva: vi.fn(),
  cancelarSolicitud: vi.fn(),
}));
vi.mock("../api/solicitudesApi", () => api);

const sesion = vi.hoisted(() => ({ permisos: [] as string[] }));
vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: { permissions: sesion.permisos },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));

const BASE: SolicitudReservaAula = {
  id: "s1",
  dia: "2026-11-20",
  horarioDesde: "08:00:00",
  horarioHasta: "10:00:00",
  cantidadAlumnosAprox: 35,
  materia: { id: "m1", codigo: "3001", nombre: "Ingeniería de Software" },
  comision: "K3001",
  estado: "pendiente",
  creadoEn: "2026-10-01T10:00:00Z",
  docente: { id: "d1", nombre: "Carla", apellido: "López" },
  esPropia: false,
};

function renderizar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={["/aulas/solicitudes/s1"]}>
        <Routes>
          <Route path="/aulas/solicitudes/:id" element={<DetalleSolicitudPage />} />
          <Route path="/aulas" element={<p>Listado de reservas</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  sesion.permisos = [];
});

describe("DetalleSolicitudPage", () => {
  it("shows the request data and goes back to the list with a real link", async () => {
    sesion.permisos = ["aulas.solicitar"];
    api.obtenerSolicitud.mockResolvedValue({ ...BASE, esPropia: true });
    renderizar();

    expect(await screen.findByRole("heading", { name: "Solicitud del 20/11/2026" })).toBeVisible();
    const datos = screen.getByRole("region", { name: "Datos de la solicitud" });
    expect(within(datos).getByText("López, Carla")).toBeInTheDocument();
    expect(within(datos).getByText("08:00 a 10:00")).toBeInTheDocument();

    const volver = screen.getByRole("link", { name: "Volver a Reserva de aulas" });
    expect(volver).toHaveAttribute("href", "/aulas");
    await userEvent.click(volver);
    expect(screen.getByText("Listado de reservas")).toBeInTheDocument();
  });

  it("explains that the request does not exist or is not accessible, with a way back", async () => {
    api.obtenerSolicitud.mockRejectedValue(new Error("404"));
    renderizar();

    expect(await screen.findByText("No se pudo cargar la solicitud")).toBeInTheDocument();
    expect(screen.getByText("La solicitud no existe o no tenés acceso.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Volver a Reserva de aulas" })).toBeInTheDocument();
  });

  it("lets the owner cancel her own pending request, with a confirmation", async () => {
    const usuario = userEvent.setup();
    sesion.permisos = ["aulas.solicitar"];
    api.obtenerSolicitud.mockResolvedValue({ ...BASE, esPropia: true });
    api.cancelarSolicitud.mockResolvedValue(undefined);
    renderizar();

    await usuario.click(await screen.findByRole("button", { name: "Cancelar solicitud" }));
    const dialogo = screen.getByRole("dialog", { name: "Cancelar solicitud" });
    // Confirmation without fields: the least destructive button holds the focus.
    expect(within(dialogo).getByRole("button", { name: "Volver" })).toHaveFocus();
    await usuario.click(within(dialogo).getByRole("button", { name: "Cancelar solicitud" }));

    expect(api.cancelarSolicitud).toHaveBeenCalledWith("s1");
    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("does not offer resolving actions to a requester who cannot approve", async () => {
    sesion.permisos = ["aulas.solicitar"];
    api.obtenerSolicitud.mockResolvedValue({
      ...BASE,
      estado: "aprobada",
      aulaAsignada: "Aula 204",
      esPropia: true,
    });
    renderizar();

    expect(await screen.findByText("Sin acciones disponibles")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Tu reserva está aprobada. Si necesitás cancelarla, pedíselo a Administración.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText(/Aula o laboratorio/)).not.toBeInTheDocument();
  });

  it("lets a resolver assign a classroom and reject, requiring the reason", async () => {
    const usuario = userEvent.setup();
    sesion.permisos = ["aulas.aprobar"];
    api.obtenerSolicitud.mockResolvedValue(BASE);
    api.asignarAula.mockResolvedValue({ ...BASE, estado: "aprobada", aulaAsignada: "Aula 204" });
    api.rechazarSolicitud.mockResolvedValue({ ...BASE, estado: "rechazada" });
    renderizar();

    await usuario.click(await screen.findByRole("button", { name: "Asignar y aprobar" }));
    expect(screen.getByText("Indicá el aula a asignar.")).toBeInTheDocument();
    expect(api.asignarAula).not.toHaveBeenCalled();

    await usuario.type(screen.getByLabelText(/Aula o laboratorio/), "Aula 204");
    await usuario.click(screen.getByRole("button", { name: "Asignar y aprobar" }));
    expect(api.asignarAula).toHaveBeenCalledWith("s1", "Aula 204");

    await usuario.click(screen.getByRole("button", { name: "Rechazar solicitud" }));
    const dialogo = screen.getByRole("dialog", { name: "Rechazar solicitud" });
    await usuario.click(within(dialogo).getByRole("button", { name: "Rechazar solicitud" }));
    expect(within(dialogo).getByText("Indicá el motivo del rechazo.")).toBeInTheDocument();
    expect(api.rechazarSolicitud).not.toHaveBeenCalled();
    await usuario.type(within(dialogo).getByLabelText(/Motivo/), "No hay aula");
    await usuario.click(within(dialogo).getByRole("button", { name: "Rechazar solicitud" }));
    expect(api.rechazarSolicitud).toHaveBeenCalledWith("s1", "No hay aula");
  });

  it("cancels an approved reservation with a mandatory reason and protects a half-typed one", async () => {
    const usuario = userEvent.setup();
    sesion.permisos = ["aulas.aprobar"];
    api.obtenerSolicitud.mockResolvedValue({
      ...BASE,
      estado: "aprobada",
      aulaAsignada: "Aula 204",
    });
    api.cancelarReserva.mockResolvedValue({ ...BASE, estado: "cancelada" });
    renderizar();

    expect(await screen.findByRole("button", { name: "Actualizar aula" })).toBeInTheDocument();
    await usuario.click(screen.getByRole("button", { name: "Cancelar reserva" }));
    const dialogo = screen.getByRole("dialog", { name: "Cancelar reserva" });
    expect(
      within(dialogo).getByText(
        "El aula asignada queda liberada, el docente ve el motivo completo y se le notifica (in-app).",
      ),
    ).toBeInTheDocument();
    expect(within(dialogo).getByLabelText(/Motivo/)).toHaveFocus();

    await usuario.click(within(dialogo).getByRole("button", { name: "Cancelar reserva" }));
    expect(within(dialogo).getByText("Indicá el motivo de la cancelación.")).toBeInTheDocument();
    expect(api.cancelarReserva).not.toHaveBeenCalled();

    await usuario.type(within(dialogo).getByLabelText(/Motivo/), "ya no se usa");
    await usuario.keyboard("{Escape}");
    expect(screen.getByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    await usuario.click(screen.getByRole("button", { name: "Seguir editando" }));

    await usuario.click(within(dialogo).getByRole("button", { name: "Cancelar reserva" }));
    expect(api.cancelarReserva).toHaveBeenCalledWith("s1", "ya no se usa");
  });

  it("shows the closing reason of a rejected and of a cancelled reservation, with no actions left", async () => {
    sesion.permisos = ["aulas.aprobar"];
    api.obtenerSolicitud.mockResolvedValue({
      ...BASE,
      estado: "cancelada",
      motivoCancelacion: "El aula quedó en mantenimiento",
    });
    renderizar();

    expect(await screen.findByText("Motivo de la cancelación")).toBeInTheDocument();
    expect(screen.getByText("El aula quedó en mantenimiento")).toBeInTheDocument();
    expect(
      screen.getByText("Esta solicitud está cerrada: no admite más acciones."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar reserva" })).not.toBeInTheDocument();
  });
});
