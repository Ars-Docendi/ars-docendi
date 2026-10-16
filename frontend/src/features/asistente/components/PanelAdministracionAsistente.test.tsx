import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PanelAdministracionAsistente } from "./PanelAdministracionAsistente";
import * as adminApi from "../api/administracionAsistenteApi";
import * as api from "../api/asistenteApi";
import * as descargas from "../utils/descargas";
import { CAPACIDADES } from "../test/soporte";
import type { PresupuestosDelAsistente, UsoAgregado, UsoDelAsistente } from "../types";

// ============================================================
// El panel administrativo del asistente (asistente-administracion-de-uso,
// tasks.md §11 y §13; en su pasada de fidelidad 1:1 con el canvas «Uso del
// asistente» de Claude Design): título propio, período relabeleado en
// pastillas, «Exportar CSV», el banner de mantenimiento compacto, KPIs
// organizacionales + tope mensual, y el detalle por usuario/rol con cupo
// editado en la fila.
//
// El comportamiento propio de cada pieza nueva vive en su propio archivo de
// test (`PanelDeUso.test.tsx`, `KpisDeUso.test.tsx`,
// `TopeOrganizacionalCard.test.tsx`, `BannerDeMantenimiento.test.tsx`,
// `EditorDeCupoEnFila.test.tsx`, `exportarUsoCsv.test.ts`); acá va la
// INTEGRACIÓN: qué compone este panel y cómo se anuncia sin robar el foco.
// ============================================================

const PRESUPUESTOS_VACIOS: PresupuestosDelAsistente = {
  topeMensualUsd: 0,
  gastoEstimadoDelMes: 0,
  esEstimado: true,
  cuposPorRol: [],
  overridesPorUsuario: [],
  accesosRevocados: [],
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
  codigosDeRol: ["docente"],
  cupoEfectivo: 15,
  origenDeCupo: "rol",
  accesoEfectivo: true,
  origenDeAcceso: "rol",
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

describe("Título de la sección (tasks.md 8.1, fidelidad con el canvas)", () => {
  // El canvas «Uso del asistente» muestra el título COMO SECCIÓN dentro de la
  // pestaña (un `<h2>`), no como una segunda página: la página «Sistema» ya
  // tiene su propio `<h1>` en `PageHeader`. Antes este panel no montaba
  // ningún título propio a propósito, para no duplicar «Uso del asistente»
  // como si fuera otro `<h1>` — ahora lo hace, pero un nivel más abajo en la
  // jerarquía, así que sigue sin haber un segundo `<h1>`.
  it("monta «Uso del asistente» como `<h2>`, nunca como un segundo `<h1>`", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPanel();

    await screen.findByRole("group", { name: "Modo mantenimiento" });
    expect(
      screen.getByRole("heading", { level: 2, name: "Uso del asistente" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });
});

describe("El período, relabeleado y en pastillas (fidelidad con el canvas)", () => {
  it("«Actualizar» de la sección Sistema refetchea con el mismo período elegido", async () => {
    const obtenerUso = vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const user = userEvent.setup();

    const { rerender, cliente } = montarPanel(0);
    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("dia"));

    await user.click(screen.getByRole("button", { name: "7 días" }));
    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("semana"));

    obtenerUso.mockClear();
    rerender(
      <QueryClientProvider client={cliente}>
        <PanelAdministracionAsistente actualizacion={1} />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("semana"));
    expect(screen.getByRole("button", { name: "7 días" })).toHaveAttribute("aria-pressed", "true");
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
    expect(within(grupo).getByRole("button", { name: "30 días" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    expect(screen.queryByRole("combobox", { name: "Período" })).not.toBeInTheDocument();
  });
});

describe("Exportar CSV (fidelidad con el canvas)", () => {
  it("exporta la vista activa (Por usuario) del período elegido, sin pedir nada nuevo al backend", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const descargarArchivo = vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = userEvent.setup();

    montarPanel();
    await screen.findByText("Marina Díaz");

    await user.click(screen.getByRole("button", { name: /Exportar CSV/ }));

    expect(descargarArchivo).toHaveBeenCalledTimes(1);
    const [nombre, contenido, tipo] = descargarArchivo.mock.calls[0];
    expect(nombre).toMatch(/^uso-asistente-dia-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(contenido).toContain("Marina Díaz");
    expect(tipo).toBe("text/csv;charset=utf-8");
  });

  it("exporta la vista activa cuando es «Por rol»", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const descargarArchivo = vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = userEvent.setup();

    montarPanel();
    await screen.findByText("Marina Díaz");
    await user.click(screen.getByRole("tab", { name: /Por rol/ }));
    await user.click(screen.getByRole("button", { name: /Exportar CSV/ }));

    const contenido = descargarArchivo.mock.calls[0][1];
    expect(contenido).toContain("docente");
    expect(contenido).not.toContain("Marina Díaz");
  });

  it("deshabilitado mientras el uso todavía no cargó", () => {
    vi.spyOn(adminApi, "obtenerUso").mockReturnValue(new Promise(() => {}));
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPanel();

    expect(screen.getByRole("button", { name: /Exportar CSV/ })).toBeDisabled();
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

    await user.click(screen.getByRole("button", { name: "7 días" }));
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
          /^de\s+US\$\s*150,50\s*\(estimado\)$/,
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

describe("El banner de mantenimiento (tasks.md 11.6, fidelidad con el canvas)", () => {
  it("no deja activar sin razón; con razón, activa", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const editarMantenimiento = vi
      .spyOn(adminApi, "editarMantenimiento")
      .mockResolvedValue({ activo: true, razon: "Mantenimiento programado" });
    const user = userEvent.setup();

    montarPanel();

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    await user.click(grupo.getByRole("button", { name: "Activar mantenimiento" }));
    const confirmar = grupo.getByRole("button", { name: "Activar mantenimiento" });
    expect(confirmar).toBeDisabled();

    await user.type(grupo.getByLabelText(/Razón/), "Mantenimiento programado");
    expect(confirmar).not.toBeDisabled();

    await user.click(confirmar);
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

    const grupo = within(await screen.findByRole("group", { name: "Modo mantenimiento" }));
    await user.click(await grupo.findByRole("button", { name: "Desactivar mantenimiento" }));

    await waitFor(() => expect(editarMantenimiento).toHaveBeenCalledWith(false, undefined));
  });
});

describe("Accesibilidad del panel (tasks.md 13.1)", () => {
  it("el período y «Activar mantenimiento» son operables por teclado", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const user = userEvent.setup();

    montarPanel();

    const semana = screen.getByRole("button", { name: "7 días" });
    semana.focus();
    expect(document.activeElement).toBe(semana);
    await user.keyboard("{Enter}");
    expect(semana).toHaveAttribute("aria-pressed", "true");

    const activar = screen.getByRole("button", { name: "Activar mantenimiento" });
    activar.focus();
    expect(document.activeElement).toBe(activar);
    await user.keyboard("{Enter}");
    expect(screen.getByLabelText(/Razón/)).toBeInTheDocument();
  });
});

describe("Los guardados se anuncian sin mover el foco (tasks.md 13.2)", () => {
  it("activar el mantenimiento anuncia y pasa el foco al botón nuevo de la rama activa", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    // El banner es controlado por `capacidades` (sin estado local propio, a
    // diferencia del toggle viejo): el refetch que dispara un guardado
    // exitoso tiene que devolver el nuevo valor para que la vista cambie de
    // rama, igual que ya hace el mock de `obtenerPresupuestos` del tope.
    vi.spyOn(api, "obtenerCapacidades")
      .mockResolvedValueOnce(CAPACIDADES)
      .mockResolvedValue({ ...CAPACIDADES, mantenimiento: { activo: true, razon: "Prueba" } });
    vi.spyOn(adminApi, "editarMantenimiento").mockResolvedValue({
      activo: true,
      razon: "Prueba",
    });
    const user = userEvent.setup();

    montarPanel();

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    await user.click(grupo.getByRole("button", { name: "Activar mantenimiento" }));
    await user.type(grupo.getByLabelText(/Razón/), "Prueba");
    const confirmar = grupo.getByRole("button", { name: "Activar mantenimiento" });
    await user.click(confirmar);

    expect(await screen.findByRole("status")).toHaveTextContent("Modo mantenimiento activado.");
    // Activar CAMBIA DE RAMA (el banner entero pasa a la vista «en
    // mantenimiento»): no hay ningún botón en común al que volver, así que el
    // foco pasa al botón nuevo de esa rama en vez de perderse en el `<body>`.
    expect(document.activeElement).toBe(
      screen.getByRole("button", { name: "Desactivar mantenimiento" }),
    );
  });

  it("guardar el cupo de una fila anuncia por la región viva compartida de la página", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(adminApi, "editarCupoDeUsuario").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPanel();

    await screen.findByText("Marina Díaz");
    await user.click(screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }));
    // El campo arranca con el cupo efectivo ya resuelto (15, del rol) — hay
    // que limpiarlo antes de escribir, si no el valor tipeado se concatena.
    await user.clear(screen.getByLabelText("Cupo diario de Marina Díaz (turnos)"));
    await user.type(screen.getByLabelText("Cupo diario de Marina Díaz (turnos)"), "20");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Cupo diario de Marina Díaz: 20 turnos.",
    );
  });
});
