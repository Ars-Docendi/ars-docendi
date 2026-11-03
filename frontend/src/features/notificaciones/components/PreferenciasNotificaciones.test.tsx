import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Toaster } from "../../../shared/ui/Toaster";
import { limpiarToasts } from "../../../shared/ui/toasts";
import { GRUPOS_PUSH } from "../push/tiposPush";
import type { DispositivoPush, EstadoDispositivo } from "../push/useDispositivoPush";
import { PreferenciasNotificaciones } from "./PreferenciasNotificaciones";

const api = vi.hoisted(() => ({
  obtenerPreferenciasPush: vi.fn(),
  guardarPreferenciasPush: vi.fn(),
}));
vi.mock("../api/pushApi", () => api);

const TODOS = GRUPOS_PUSH.flatMap((g) => g.tipos.map((t) => t.id));

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
    tipos: TODOS.map((tipoEvento) => ({
      tipoEvento,
      habilitado: tipoEvento !== "TAREA_COMENTADA",
    })),
  });
  api.guardarPreferenciasPush.mockResolvedValue({ tipos: [] });
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

  it("sin soporte o con el servidor apagado: lo dice y no ofrece los avisos por tipo", () => {
    renderizar(dispositivo("servidor-apagado"));
    expect(screen.getByText(/No disponibles por ahora/)).toBeInTheDocument();
    expect(interruptorDispositivo()).toBeDisabled();
    expect(screen.queryByRole("group")).not.toBeInTheDocument();
    expect(api.obtenerPreferenciasPush).not.toHaveBeenCalled();
  });

  it("muestra el error de una activación fallida", () => {
    renderizar(dispositivo("desactivado", { error: "No pudimos activar las notificaciones." }));
    expect(screen.getByRole("alert")).toHaveTextContent("No pudimos activar");
  });
});

describe("interruptores por tipo de aviso", () => {
  it("con el dispositivo activo reflejan lo guardado y cada cambio guarda sólo ese tipo", async () => {
    renderizar(dispositivo("activo"));
    const comentario = await screen.findByRole("switch", { name: "Comentario nuevo" });
    await waitFor(() => expect(comentario).toBeEnabled());
    expect(comentario).not.toBeChecked();
    expect(screen.getByRole("switch", { name: "Aula asignada" })).toBeChecked();

    await userEvent.click(screen.getByRole("switch", { name: "Aula asignada" }));
    expect(api.guardarPreferenciasPush).toHaveBeenCalledWith([
      { tipoEvento: "AULA_ASIGNADA", habilitado: false },
    ]);
    expect(screen.getByRole("switch", { name: "Aula asignada" })).not.toBeChecked();
  });

  it("si guardar falla, vuelve atrás y avisa", async () => {
    api.guardarPreferenciasPush.mockRejectedValue(new Error("500"));
    renderizar(dispositivo("activo"));
    const aula = await screen.findByRole("switch", { name: "Aula asignada" });
    await waitFor(() => expect(aula).toBeEnabled());

    await userEvent.click(aula);
    expect(await screen.findByText("No pudimos guardar el cambio")).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole("switch", { name: "Aula asignada" })).toBeChecked(),
    );
  });

  it("con el dispositivo desactivado están deshabilitados y se pide activarlo primero", async () => {
    renderizar(dispositivo("desactivado"));
    const aula = await screen.findByRole("switch", { name: "Aula asignada" });
    expect(aula).toBeDisabled();
    expect(
      screen.getByText(/Activá las notificaciones en este dispositivo para elegir/),
    ).toBeInTheDocument();
    await userEvent.click(aula);
    expect(api.guardarPreferenciasPush).not.toHaveBeenCalled();
  });

  it("sólo ofrece los módulos que el rol activo puede recibir", async () => {
    renderizar(dispositivo("activo"), ["tareas.ver", "portal.ver"]);
    await screen.findByRole("switch", { name: "Tarea asignada" });
    expect(screen.getByRole("group", { name: "Tareas" })).toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Reserva de aulas" })).not.toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Designaciones" })).not.toBeInTheDocument();
  });

  it("sin lista de permisos muestra los tres módulos con todos sus tipos", async () => {
    renderizar(dispositivo("activo"));
    await screen.findByRole("switch", { name: "Aula asignada" });
    expect(screen.getAllByRole("group").map((g) => g.getAttribute("aria-labelledby"))).toHaveLength(
      3,
    );
    const total = screen.getAllByRole("switch").length - 1; // minus the device switch
    expect(total).toBe(TODOS.length);
    expect(
      within(screen.getByRole("group", { name: "Designaciones" })).getAllByRole("switch"),
    ).toHaveLength(3);
  });
});
