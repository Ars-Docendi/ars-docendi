import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Toaster } from "../../../shared/ui/Toaster";
import { limpiarToasts } from "../../../shared/ui/toasts";
import { MODULOS_PUSH } from "../push/modulosPush";
import type { DispositivoPush, EstadoDispositivo } from "../push/useDispositivoPush";
import { PreferenciasNotificaciones } from "./PreferenciasNotificaciones";

const api = vi.hoisted(() => ({
  obtenerPreferenciasPush: vi.fn(),
  guardarPreferenciasPush: vi.fn(),
}));
vi.mock("../api/pushApi", () => api);

function dispositivo(
  estado: EstadoDispositivo,
  extra: Partial<DispositivoPush> = {},
): DispositivoPush {
  return {
    estado,
    trabajando: false,
    error: null,
    activar: vi.fn().mockResolvedValue(undefined),
    desactivar: vi.fn().mockResolvedValue(undefined),
    ...extra,
  };
}

function renderizar(disp: DispositivoPush, permisos?: readonly string[]) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <MemoryRouter>
      <QueryClientProvider client={cliente}>
        <PreferenciasNotificaciones dispositivo={disp} permisos={permisos} />
        <Toaster />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

const interruptorDispositivo = () =>
  screen.getByRole("switch", { name: "Notificaciones en este dispositivo" });

beforeEach(() => {
  vi.clearAllMocks();
  limpiarToasts();
  api.obtenerPreferenciasPush.mockResolvedValue({
    modulos: MODULOS_PUSH.map(({ id }) => ({ modulo: id, habilitado: id !== "TAREAS" })),
  });
  api.guardarPreferenciasPush.mockResolvedValue({ modulos: [] });
});

describe("interruptor del dispositivo", () => {
  it("activo: está encendido y apagarlo desactiva este dispositivo", async () => {
    const disp = dispositivo("activo");
    renderizar(disp);
    expect(interruptorDispositivo()).toBeChecked();
    expect(screen.getByText(/Activas\./)).toBeInTheDocument();

    await userEvent.click(interruptorDispositivo());
    expect(disp.desactivar).toHaveBeenCalledOnce();
  });

  it("desactivado: encenderlo lo activa", async () => {
    const disp = dispositivo("desactivado");
    renderizar(disp);
    expect(interruptorDispositivo()).not.toBeChecked();
    await userEvent.click(interruptorDispositivo());
    expect(disp.activar).toHaveBeenCalledOnce();
  });

  it("bloqueado en el navegador: explica cómo desbloquear y no se puede encender", () => {
    renderizar(dispositivo("bloqueado"));
    expect(screen.getByText(/Bloqueadas en el navegador/)).toHaveTextContent(
      /configuración del navegador/,
    );
    expect(interruptorDispositivo()).toBeDisabled();
  });

  it("iPhone sin instalar: no deja la pantalla vacía, explica la pantalla de inicio y enlaza la ayuda", () => {
    renderizar(dispositivo("ios-sin-instalar"));
    expect(screen.getByText(/Agregar a pantalla de inicio/)).toBeInTheDocument();
    expect(interruptorDispositivo()).toBeDisabled();
    expect(screen.getByRole("link", { name: "¿Cómo activarlas en el celular?" })).toHaveAttribute(
      "href",
      "/ayuda/primeros-pasos/activar-las-notificaciones-en-el-celular",
    );
  });

  it("sin soporte o con el servidor apagado: lo dice y no ofrece los avisos por módulo", () => {
    renderizar(dispositivo("servidor-apagado"));
    expect(screen.getByText(/No disponibles por ahora/)).toBeInTheDocument();
    expect(interruptorDispositivo()).toBeDisabled();
    expect(screen.queryByRole("switch", { name: "Tareas" })).not.toBeInTheDocument();
    expect(api.obtenerPreferenciasPush).not.toHaveBeenCalled();
  });

  it("muestra el error de una activación fallida", () => {
    renderizar(dispositivo("desactivado", { error: "No pudimos activar las notificaciones." }));
    expect(screen.getByRole("alert")).toHaveTextContent("No pudimos activar");
  });
});

describe("interruptores por módulo", () => {
  it("con el dispositivo activo reflejan lo guardado y cada cambio guarda sólo ese módulo", async () => {
    renderizar(dispositivo("activo"));
    const tareas = await screen.findByRole("switch", { name: "Tareas" });
    await waitFor(() => expect(tareas).toBeEnabled());
    expect(tareas).not.toBeChecked();
    expect(screen.getByRole("switch", { name: "Reserva de aulas" })).toBeChecked();
    expect(
      screen.getByText("Asignaciones, cambios, rechazos y cancelaciones de tus reservas."),
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole("switch", { name: "Reserva de aulas" }));
    expect(api.guardarPreferenciasPush).toHaveBeenCalledWith([
      { modulo: "AULAS", habilitado: false },
    ]);
    expect(screen.getByRole("switch", { name: "Reserva de aulas" })).not.toBeChecked();
  });

  it("si guardar falla, vuelve atrás y avisa", async () => {
    api.guardarPreferenciasPush.mockRejectedValue(new Error("500"));
    renderizar(dispositivo("activo"));
    const aulas = await screen.findByRole("switch", { name: "Reserva de aulas" });
    await waitFor(() => expect(aulas).toBeEnabled());

    await userEvent.click(aulas);
    expect(await screen.findByText("No pudimos guardar el cambio")).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole("switch", { name: "Reserva de aulas" })).toBeChecked(),
    );
  });

  it("con el dispositivo desactivado están deshabilitados y se pide activarlo primero", async () => {
    renderizar(dispositivo("desactivado"));
    const aulas = await screen.findByRole("switch", { name: "Reserva de aulas" });
    expect(aulas).toBeDisabled();
    expect(
      screen.getByText(/Activá las notificaciones en este dispositivo para elegir/),
    ).toBeInTheDocument();
    await userEvent.click(aulas);
    expect(api.guardarPreferenciasPush).not.toHaveBeenCalled();
  });

  it("sólo ofrece los módulos que el rol activo puede recibir", async () => {
    renderizar(dispositivo("activo"), ["tareas.ver", "portal.ver"]);
    await screen.findByRole("switch", { name: "Tareas" });
    expect(screen.queryByRole("switch", { name: "Reserva de aulas" })).not.toBeInTheDocument();
    expect(screen.queryByRole("switch", { name: "Designaciones" })).not.toBeInTheDocument();
  });

  it("sin lista de permisos muestra los tres módulos", async () => {
    renderizar(dispositivo("activo"));
    await screen.findByRole("switch", { name: "Reserva de aulas" });
    const interruptores = screen.getAllByRole("switch").slice(1); // after the device switch
    expect(interruptores.map((i) => i.getAttribute("id"))).toEqual([
      "notif-prefs-modulo-AULAS",
      "notif-prefs-modulo-DESIGNACIONES",
      "notif-prefs-modulo-TAREAS",
    ]);
  });
});
