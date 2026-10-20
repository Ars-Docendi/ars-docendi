import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PestanaEstado } from "./PestanaEstado";
import * as sistemaApi from "../api/sistemaApi";
import * as auditoriaApi from "../api/auditoriaApi";
import type { EstadoInferenciaDto, ResultadoPing, EstadoSistemaDto } from "../api/sistemaApi";
import type { PaginaAuditoria } from "../api/auditoriaApi";

afterEach(() => vi.restoreAllMocks());

const PAGINA_VACIA: PaginaAuditoria = {
  elementos: [],
  pagina: 1,
  tamanoPagina: 4,
  total: 0,
  parcial: false,
  fuentesNoDisponibles: [],
};

function ping(id: ResultadoPing["id"], disponible: boolean, duracionMs = 40): ResultadoPing {
  return { id, nombre: id, disponible, duracionMs, comprobadoEn: new Date().toISOString() };
}

const ESTADO_OK: EstadoSistemaDto = {
  estado: "disponible",
  comprobadoEn: new Date().toISOString(),
  duracionMs: 12,
  mantenimientoAsistente: "inactivo",
};

function mockearInferencia(estado: EstadoInferenciaDto["estado"] = "disponible") {
  return vi.spyOn(sistemaApi, "consultarEstadoInferencia").mockResolvedValue({
    estado,
    comprobadoEn: new Date().toISOString(),
    duracionMs: estado === "deshabilitado" ? 0 : 25,
  });
}

function mockearTodoDisponible() {
  vi.spyOn(sistemaApi, "comprobarPing").mockImplementation(async (c) => ping(c.id, true));
  vi.spyOn(sistemaApi, "consultarEstadoSistema").mockResolvedValue(ESTADO_OK);
  mockearInferencia();
  vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue(PAGINA_VACIA);
}

function tarjeta(nombre: string): HTMLElement {
  return screen.getByText(nombre).closest(".sistema-tarjeta") as HTMLElement;
}

function montar(props: Partial<Parameters<typeof PestanaEstado>[0]> = {}) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const onVerUso = vi.fn();
  const onAbrirCambio = vi.fn();
  const onVerTodaLaAuditoria = vi.fn();
  const utils = render(
    <QueryClientProvider client={cliente}>
      <PestanaEstado
        puedeVerUso={props.puedeVerUso ?? true}
        puedeVerAuditoria={props.puedeVerAuditoria ?? true}
        onVerUso={onVerUso}
        onAbrirCambio={onAbrirCambio}
        onVerTodaLaAuditoria={onVerTodaLaAuditoria}
      />
    </QueryClientProvider>,
  );
  return { ...utils, onVerUso, onAbrirCambio, onVerTodaLaAuditoria };
}

describe("PestanaEstado", () => {
  it("todos disponibles: banner ok con 7 de 7", async () => {
    mockearTodoDisponible();

    montar();

    expect(await screen.findByText("Todos los componentes disponibles")).toBeInTheDocument();
    expect(screen.getByText("7 de 7 responden con normalidad.")).toBeInTheDocument();
  });

  it("inferencia disponible: tarjeta en Infraestructura con su tiempo de respuesta", async () => {
    mockearTodoDisponible();

    montar();

    await screen.findByText("Todos los componentes disponibles");
    const infraestructura = screen.getByRole("region", { name: "Infraestructura" });
    const inferencia = within(infraestructura)
      .getByText("Inferencia (IA)")
      .closest(".sistema-tarjeta") as HTMLElement;
    expect(within(inferencia).getByText("Disponible")).toBeInTheDocument();
    expect(within(inferencia).getByText("25 ms")).toBeInTheDocument();
  });

  it("inferencia deshabilitada: estado neutral, sin Reintentar y sin degradar el banner", async () => {
    mockearTodoDisponible();
    mockearInferencia("deshabilitado");

    montar();

    expect(await screen.findByText("Todos los componentes disponibles")).toBeInTheDocument();
    expect(screen.getByText("6 de 6 responden con normalidad.")).toBeInTheDocument();
    const inferencia = tarjeta("Inferencia (IA)");
    expect(inferencia).toHaveClass("sistema-tarjeta--deshabilitado");
    expect(inferencia).not.toHaveClass("sistema-tarjeta--no_disponible");
    expect(within(inferencia).getByText("Deshabilitado")).toHaveClass(
      "sistema-pill--deshabilitado",
    );
    expect(within(inferencia).getByText("No configurada en este ambiente")).toBeInTheDocument();
    expect(within(inferencia).queryByText(/ms$/)).not.toBeInTheDocument();
    expect(within(inferencia).queryByRole("button", { name: "Reintentar" })).toBeNull();
  });

  it("inferencia no disponible: cuenta como falla, la nombra y se reintenta sola", async () => {
    mockearTodoDisponible();
    const espiaInferencia = mockearInferencia("no_disponible");
    const user = userEvent.setup();

    montar();

    expect(await screen.findByText("1 componente no disponible")).toBeInTheDocument();
    expect(
      screen.getByText("Inferencia (IA) no respondió. El resto funciona con normalidad."),
    ).toBeInTheDocument();
    const inferencia = tarjeta("Inferencia (IA)");
    expect(within(inferencia).getByText("No disponible")).toHaveClass(
      "sistema-pill--no_disponible",
    );
    expect(within(tarjeta("PostgreSQL")).getByText("Disponible")).toBeInTheDocument();
    const llamadasPing = vi.mocked(sistemaApi.comprobarPing).mock.calls.length;

    await user.click(within(inferencia).getByRole("button", { name: "Reintentar" }));

    await waitFor(() => expect(espiaInferencia).toHaveBeenCalledTimes(2));
    expect(sistemaApi.comprobarPing).toHaveBeenCalledTimes(llamadasPing);
  });

  it("«Última comprobación» usa el formato del canvas: d/m/aaaa HH:mm:ss de 24 horas (defecto #2, task 11.4)", async () => {
    // Pinned instant: 26/9/2026 22:16:48 en Buenos Aires (UTC-3) == 27/9 01:16:48Z.
    const comprobadoEn = "2026-09-27T01:16:48.000Z";
    vi.spyOn(sistemaApi, "comprobarPing").mockImplementation(async (c) => ({
      ...ping(c.id, true),
      comprobadoEn,
    }));
    vi.spyOn(sistemaApi, "consultarEstadoSistema").mockResolvedValue({
      ...ESTADO_OK,
      comprobadoEn,
    });
    vi.spyOn(sistemaApi, "consultarEstadoInferencia").mockResolvedValue({
      estado: "disponible",
      comprobadoEn,
      duracionMs: 25,
    });
    vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue(PAGINA_VACIA);

    montar();

    // `toLocaleString("es-AR", {dateStyle:"short", timeStyle:"medium"})` daría
    // «27/9/26, 10:16:48 p. m.» (año de 2 dígitos, coma, 12 horas con «p. m.»).
    expect(await screen.findByText("26/9/2026 22:16:48")).toBeInTheDocument();
  });

  it("reintentar una tarjeta vuelve a pedir sólo ese componente", async () => {
    let llamadasTareas = 0;
    vi.spyOn(sistemaApi, "comprobarPing").mockImplementation(async (c) => {
      if (c.id === "tareas") llamadasTareas += 1;
      return ping(c.id, c.id !== "tareas");
    });
    vi.spyOn(sistemaApi, "consultarEstadoSistema").mockResolvedValue(ESTADO_OK);
    mockearInferencia();
    vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue(PAGINA_VACIA);
    const user = userEvent.setup();

    montar();

    await screen.findByText("1 componente no disponible");
    expect(llamadasTareas).toBe(1);

    const tarjetaTareas = screen.getByText("Tareas").closest(".sistema-tarjeta") as HTMLElement;
    await user.click(within(tarjetaTareas).getByRole("button", { name: "Reintentar" }));

    await waitFor(() => expect(llamadasTareas).toBe(2));
  });

  it("«Ver uso →» sólo aparece con asistente.administrar y cambia de pestaña al presionarlo", async () => {
    mockearTodoDisponible();
    const user = userEvent.setup();

    const { onVerUso } = montar({ puedeVerUso: true });
    const tarjetaAsistente = (await screen.findByText("Asistente")).closest(
      ".sistema-tarjeta",
    ) as HTMLElement;
    const boton = within(tarjetaAsistente).getByRole("button", { name: "Ver uso →" });
    await user.click(boton);
    expect(onVerUso).toHaveBeenCalled();
  });

  it("sin asistente.administrar no muestra «Ver uso →»", async () => {
    mockearTodoDisponible();

    montar({ puedeVerUso: false });

    const tarjetaAsistente = (await screen.findByText("Asistente")).closest(
      ".sistema-tarjeta",
    ) as HTMLElement;
    expect(within(tarjetaAsistente).queryByRole("button", { name: "Ver uso →" })).toBeNull();
  });

  it("sin auditoria.ver no se monta «Cambios recientes» ni se llama a la API", async () => {
    mockearTodoDisponible();

    montar({ puedeVerAuditoria: false });

    await screen.findByText("Todos los componentes disponibles");
    expect(screen.queryByText("Cambios recientes")).not.toBeInTheDocument();
    expect(auditoriaApi.listarAuditoria).not.toHaveBeenCalled();
  });
});
