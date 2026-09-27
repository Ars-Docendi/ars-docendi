import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PanelAdministracionAsistente } from "./PanelAdministracionAsistente";
import * as adminApi from "../api/administracionAsistenteApi";
import * as api from "../api/asistenteApi";
import { CAPACIDADES } from "../test/soporte";
import type { PresupuestosDelAsistente, UsoAgregado, UsoDelAsistente } from "../types";

// ============================================================
// El panel administrativo del asistente (asistente-administracion-de-uso,
// tasks.md §11 y §13; rediseñado 1:1 con la referencia «Uso del asistente»,
// sistema-seccion-unificada): período en pastillas, KPIs organizacionales +
// tope mensual, y el detalle por usuario/rol con cupo editado en la fila.
//
// El comportamiento propio de cada pieza nueva vive en su propio archivo de
// test (`PanelDeUso.test.tsx`, `KpisDeUso.test.tsx`,
// `TopeOrganizacionalCard.test.tsx`, `EditorDeCupoEnFila.test.tsx`); acá va
// la INTEGRACIÓN: qué compone este panel y cómo se anuncia sin robar el foco.
// ============================================================

const PRESUPUESTOS_VACIOS: PresupuestosDelAsistente = {
  topeMensualUsd: 0,
  gastoEstimadoDelMes: 0,
  esEstimado: true,
  cuposPorRol: [],
  overridesPorUsuario: [],
};

// `GET …/presupuestos` (tarea 12.8) se pide en TODAS las variantes del panel:
// un default vacío evita que cada test que no lo ejercita tenga que
// mockearlo aparte. Los tests de la propia tarjeta/tabla lo pisan.
beforeEach(() => {
  vi.spyOn(adminApi, "obtenerPresupuestos").mockResolvedValue(PRESUPUESTOS_VACIOS);
});

afterEach(() => {
  vi.restoreAllMocks();
});

function montarPanel(actualizacion?: number) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return {
    cliente,
    ...render(
      <QueryClientProvider client={cliente}>
        <PanelAdministracionAsistente actualizacion={actualizacion} />
      </QueryClientProvider>,
    ),
  };
}

const FILA_USUARIO: UsoAgregado = {
  clave: "11111111-1111-4111-8111-111111111111",
  nombreParaMostrar: "Marina Díaz",
  turnos: 12,
  porEstado: { respondida: 10, servicio_degradado: 2 },
  llamadasAlModelo: 20,
  tokensDeEntrada: 4000,
  tokensDeSalida: 1200,
  tokensDeCache: 300,
  latenciaPromedioMs: 850,
  latenciaP95Ms: 1500,
  proveedores: ["anthropic"],
  costoEstimado: 0.42,
  esEstimado: true,
  turnosSinPrecio: 0,
};

const FILA_ROL: UsoAgregado = {
  ...FILA_USUARIO,
  clave: "docente",
  nombreParaMostrar: null,
  turnosSinPrecio: 3,
  costoEstimado: 0.1,
};

const ORGANIZACION: UsoAgregado = {
  ...FILA_USUARIO,
  clave: "organizacion",
  nombreParaMostrar: null,
};

const USO_CON_DATOS: UsoDelAsistente = {
  porUsuario: [FILA_USUARIO],
  porRol: [FILA_ROL],
  organizacion: ORGANIZACION,
};

const USO_VACIO: UsoDelAsistente = {
  porUsuario: [],
  porRol: [],
  organizacion: { ...ORGANIZACION, turnos: 0, turnosSinPrecio: 0, costoEstimado: 0 },
};

describe("Sin encabezado propio (tasks.md 8.1)", () => {
  it("no monta un segundo título «Uso del asistente»: la pestaña ya trae el suyo", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPanel();

    await screen.findByRole("group", { name: "Modo mantenimiento" });
    expect(screen.queryByText("Uso del asistente")).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });
});

describe("El período, en pastillas (rediseño «Uso del asistente»)", () => {
  it("«Actualizar» de la sección Sistema refetchea con el mismo período elegido", async () => {
    const obtenerUso = vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const user = userEvent.setup();

    const { rerender, cliente } = montarPanel(0);
    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("dia"));

    await user.click(screen.getByRole("button", { name: "Última semana" }));
    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("semana"));

    obtenerUso.mockClear();
    rerender(
      <QueryClientProvider client={cliente}>
        <PanelAdministracionAsistente actualizacion={1} />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("semana"));
    expect(screen.getByRole("button", { name: "Última semana" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("es un grupo de botones, no un `<select>` de ancho completo", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPanel();

    const grupo = screen.getByRole("group", { name: "Período" });
    expect(within(grupo).getByRole("button", { name: "Hoy" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(within(grupo).getByRole("button", { name: "Último mes" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    expect(screen.queryByRole("combobox", { name: "Período" })).not.toBeInTheDocument();
  });
});

describe("El panel de uso (tasks.md 11.3)", () => {
  it("muestra los KPIs organizacionales y el detalle del período elegido", async () => {
    const obtenerUso = vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const user = userEvent.setup();

    montarPanel();

    expect(await screen.findByText("Marina Díaz")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Organización" })).toBeInTheDocument();
    expect(
      within(screen.getByRole("region", { name: "Organización" })).getByText("Sesiones"),
    ).toBeInTheDocument();
    expect(obtenerUso).toHaveBeenCalledWith("dia");

    await user.click(screen.getByRole("button", { name: "Última semana" }));
    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("semana"));
  });

  it("un período sin datos no rompe: se ve el aviso de vacío en el detalle", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPanel();

    expect(await screen.findByText("No hay uso registrado en este período.")).toBeInTheDocument();
  });
});

describe("Tope organizacional mensual (tasks.md 11.5, tarea 12.8)", () => {
  it("editarlo llama al endpoint propio, refresca lo persistido y lo anuncia sin robar el foco", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    // Antes del guardado el GET no tiene tope; después de invalidar la query
    // (el panel refresca tras cada PUT exitoso) refleja el valor persistido —
    // ya NO hace falta un estado local "guardado en esta sesión".
    vi.spyOn(adminApi, "obtenerPresupuestos")
      .mockResolvedValueOnce(PRESUPUESTOS_VACIOS)
      .mockResolvedValue({ ...PRESUPUESTOS_VACIOS, topeMensualUsd: 150.5 });
    const editarTopeOrganizacional = vi
      .spyOn(adminApi, "editarTopeOrganizacional")
      .mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPanel();

    await user.click(await screen.findByRole("button", { name: "Editar tope" }));
    await user.type(screen.getByLabelText("Tope mensual (USD)"), "150.5");
    const guardar = screen.getByRole("button", { name: "Guardar" });
    guardar.focus();
    await user.click(guardar);

    await waitFor(() => expect(editarTopeOrganizacional).toHaveBeenCalledWith(150.5));
    const status = await screen.findByRole("status");
    await waitFor(() =>
      expect(status.textContent).toMatch(/Tope organizacional mensual: US\$\s*150,50\./),
    );
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Editar tope" }));
    await waitFor(() =>
      expect(
        within(screen.getByRole("group", { name: "Tope organizacional mensual" })).getByText(
          "US$ 150,50",
        ),
      ).toBeInTheDocument(),
    );
  });

  it("muestra el gasto del mes contra el tope, coloreado por umbral", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(adminApi, "obtenerPresupuestos").mockResolvedValue({
      ...PRESUPUESTOS_VACIOS,
      topeMensualUsd: 100,
      gastoEstimadoDelMes: 85,
    });

    montarPanel();

    expect(await screen.findByRole("progressbar", { name: /Gasto del mes/ })).toHaveAttribute(
      "aria-valuenow",
      "85",
    );
  });
});

describe("El toggle de mantenimiento (tasks.md 11.6)", () => {
  it("no deja guardar la activación sin razón; con razón, guarda", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const editarMantenimiento = vi
      .spyOn(adminApi, "editarMantenimiento")
      .mockResolvedValue({ activo: true, razon: "Mantenimiento programado" });
    const user = userEvent.setup();

    montarPanel();

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    const checkbox = grupo.getByRole("switch", { name: "Asistente en mantenimiento" });
    const guardar = grupo.getByRole("button", { name: "Guardar cambios" });

    await user.click(checkbox);
    expect(guardar).toBeDisabled();

    await user.click(guardar);
    expect(editarMantenimiento).not.toHaveBeenCalled();

    await user.type(grupo.getByLabelText(/Razón/), "Mantenimiento programado");
    expect(guardar).not.toBeDisabled();

    await user.click(guardar);
    await waitFor(() =>
      expect(editarMantenimiento).toHaveBeenCalledWith(true, "Mantenimiento programado"),
    );
  });

  it("desactivar no exige razón", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue({
      ...CAPACIDADES,
      mantenimiento: { activo: true, razon: "Mantenimiento programado" },
    });
    const editarMantenimiento = vi
      .spyOn(adminApi, "editarMantenimiento")
      .mockResolvedValue({ activo: false, razon: null });
    const user = userEvent.setup();

    montarPanel();

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    const checkbox = await grupo.findByRole("switch", { name: "Asistente en mantenimiento" });
    await waitFor(() => expect(checkbox).toBeChecked());

    await user.click(checkbox);
    const guardar = grupo.getByRole("button", { name: "Guardar cambios" });
    expect(guardar).not.toBeDisabled();

    await user.click(guardar);
    await waitFor(() => expect(editarMantenimiento).toHaveBeenCalledWith(false, undefined));
  });
});

describe("Accesibilidad del panel (tasks.md 13.1)", () => {
  it("el período y el toggle de mantenimiento son operables por teclado", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const user = userEvent.setup();

    montarPanel();

    const semana = screen.getByRole("button", { name: "Última semana" });
    semana.focus();
    expect(document.activeElement).toBe(semana);
    await user.keyboard("{Enter}");
    expect(semana).toHaveAttribute("aria-pressed", "true");

    const checkbox = screen.getByRole("switch", { name: "Asistente en mantenimiento" });
    checkbox.focus();
    expect(document.activeElement).toBe(checkbox);
    await user.keyboard(" ");
    expect(checkbox).toBeChecked();
    await user.keyboard(" ");
    expect(checkbox).not.toBeChecked();
  });
});

describe("Los guardados se anuncian sin mover el foco (tasks.md 13.2)", () => {
  it("completar el toggle de mantenimiento anuncia sin mover el foco", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(adminApi, "editarMantenimiento").mockResolvedValue({
      activo: true,
      razon: "Prueba",
    });
    const user = userEvent.setup();

    montarPanel();

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    await user.click(grupo.getByRole("switch", { name: "Asistente en mantenimiento" }));
    await user.type(grupo.getByLabelText(/Razón/), "Prueba");
    const guardar = grupo.getByRole("button", { name: "Guardar cambios" });
    guardar.focus();
    await user.click(guardar);

    expect(await screen.findByRole("status")).toHaveTextContent("Modo mantenimiento activado.");
    expect(document.activeElement).toBe(guardar);
  });

  it("guardar el cupo de una fila anuncia por la región viva compartida de la página", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(adminApi, "editarCupoDeUsuario").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPanel();

    await screen.findByText("Marina Díaz");
    await user.click(screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }));
    await user.type(screen.getByLabelText("Cupo diario de Marina Díaz (turnos)"), "20");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Cupo diario de Marina Díaz: 20 turnos.",
    );
  });
});
