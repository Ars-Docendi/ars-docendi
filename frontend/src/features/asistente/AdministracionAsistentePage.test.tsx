import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router-dom";

import { routes } from "./routes";
import { AdministracionAsistentePage } from "./pages/AdministracionAsistentePage";
import * as adminApi from "./api/administracionAsistenteApi";
import * as api from "./api/asistenteApi";
import { CAPACIDADES } from "./test/soporte";
import type { UsoAgregado, UsoDelAsistente } from "./types";
import * as useCurrentUserMod from "../../shared/auth/useCurrentUser";

// ============================================================
// El panel administrativo del asistente (asistente-administracion-de-uso,
// tasks.md §11 y §13).
// ============================================================

vi.mock("../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

function conUsuario(permissions: string[]) {
  vi.mocked(useCurrentUserMod.useCurrentUser).mockReturnValue({
    user: {
      name: "x",
      initials: "X",
      upn: "x@unlam.edu.ar",
      role: "r",
      roleCode: "r",
      permissions,
    },
    isLoading: false,
    error: null,
    retry: () => undefined,
  });
}

afterEach(() => {
  vi.restoreAllMocks();
});

function montarPagina() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={cliente}>
      <AdministracionAsistentePage />
    </QueryClientProvider>,
  );
}

function montarConRouter(entrada: string) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const router = createMemoryRouter([{ path: "/", element: <p>Inicio</p> }, routes], {
    initialEntries: [entrada],
  });
  return render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
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

describe("La ruta, sin el permiso (tasks.md 11.1)", () => {
  it("no hay match de ruta: navegar a la URL no muestra el panel", async () => {
    conUsuario([]);

    montarConRouter("/asistente/administracion");

    await waitFor(() => expect(screen.queryByText("Uso del asistente")).not.toBeInTheDocument());
  });
});

describe("La ruta, con el permiso (tasks.md 11.1)", () => {
  it("el panel se monta", async () => {
    conUsuario(["asistente.administrar"]);
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarConRouter("/asistente/administracion");

    expect(await screen.findByText("Uso del asistente")).toBeInTheDocument();
  });
});

describe("El panel de uso (tasks.md 11.3)", () => {
  it("muestra los totales por usuario, por rol y organizacional del período elegido", async () => {
    const obtenerUso = vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const user = userEvent.setup();

    montarPagina();

    expect(await screen.findByText("Marina Díaz")).toBeInTheDocument();
    expect(screen.getByText("docente")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Organización" })).toBeInTheDocument();
    expect(obtenerUso).toHaveBeenCalledWith("dia");

    await user.selectOptions(screen.getByRole("combobox", { name: "Período" }), "semana");
    await waitFor(() => expect(obtenerUso).toHaveBeenCalledWith("semana"));
  });

  it("un período sin datos no rompe: se ve el aviso de vacío en las tres tablas", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPagina();

    expect(await screen.findAllByText("No hay uso registrado en este período.")).toHaveLength(2); // por usuario y por rol; la organización sí tiene una fila (0 turnos)
  });
});

describe("El costo estimado nunca se ve como cero silencioso (tasks.md 11.4)", () => {
  it("lleva la etiqueta «(estimado)» y muestra la fila con turnos sin precio aparte", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_CON_DATOS);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarPagina();

    await screen.findByText("docente");
    const filaDelRol = screen.getByText("docente").closest("tr")!;
    expect(within(filaDelRol).getByText(/\(estimado\)/)).toBeInTheDocument();
    expect(within(filaDelRol).getByText("3 turnos sin precio")).toBeInTheDocument();
  });
});

describe("Editar presupuestos (tasks.md 11.5)", () => {
  it("el primer guardado no pide confirmación; bajar el valor sí", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const editarCupoDeRol = vi.spyOn(adminApi, "editarCupoDeRol").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPagina();

    const editor = within(screen.getByRole("group", { name: "Cupo diario por rol" }));

    await user.type(editor.getByLabelText("Código de rol"), "docente");
    await user.type(editor.getByLabelText("Cupo diario (turnos)"), "20");
    await user.click(editor.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(editarCupoDeRol).toHaveBeenCalledWith("docente", 20));
    expect(screen.queryByText(/¿Bajar/)).not.toBeInTheDocument();

    await user.clear(editor.getByLabelText("Cupo diario (turnos)"));
    await user.type(editor.getByLabelText("Cupo diario (turnos)"), "10");
    await user.click(editor.getByRole("button", { name: "Guardar" }));

    // La confirmación INLINE reemplaza el formulario en el mismo lugar —mismo
    // patrón que «Borrar» en el cajón de historial—, no un diálogo aparte.
    const grupo = within(screen.getByRole("group", { name: "Cupo diario por rol" }));
    expect(await grupo.findByText(/¿Bajar.*de 20 a 10\?/)).toBeInTheDocument();

    await user.click(grupo.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(editarCupoDeRol).toHaveBeenCalledWith("docente", 10));
    expect(editarCupoDeRol).toHaveBeenCalledTimes(2);
  });

  it("el override de usuario y el tope organizacional llaman a sus propios endpoints", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const editarCupoDeUsuario = vi
      .spyOn(adminApi, "editarCupoDeUsuario")
      .mockResolvedValue(undefined);
    const editarTopeOrganizacional = vi
      .spyOn(adminApi, "editarTopeOrganizacional")
      .mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPagina();

    const editorUsuario = within(
      screen.getByRole("group", { name: "Override de cupo por usuario" }),
    );
    await user.type(
      editorUsuario.getByLabelText("Id del usuario"),
      "22222222-2222-4222-8222-222222222222",
    );
    await user.type(editorUsuario.getByLabelText("Cupo diario (turnos)"), "40");
    await user.click(editorUsuario.getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(editarCupoDeUsuario).toHaveBeenCalledWith("22222222-2222-4222-8222-222222222222", 40),
    );

    const editorTope = within(screen.getByRole("group", { name: "Tope organizacional mensual" }));
    await user.type(editorTope.getByLabelText("Tope mensual (USD)"), "150.5");
    await user.click(editorTope.getByRole("button", { name: "Guardar" }));
    await waitFor(() => expect(editarTopeOrganizacional).toHaveBeenCalledWith(150.5));
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

    montarPagina();

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

    montarPagina();

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
  it("el período, el toggle de mantenimiento y los editores son operables por teclado", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const editarCupoDeRol = vi.spyOn(adminApi, "editarCupoDeRol").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPagina();

    const periodo = await screen.findByRole("combobox", { name: "Período" });
    periodo.focus();
    expect(document.activeElement).toBe(periodo);

    const checkbox = screen.getByRole("switch", { name: "Asistente en mantenimiento" });
    checkbox.focus();
    expect(document.activeElement).toBe(checkbox);
    await user.keyboard(" ");
    expect(checkbox).toBeChecked();
    await user.keyboard(" ");
    expect(checkbox).not.toBeChecked();

    const editor = within(screen.getByRole("group", { name: "Cupo diario por rol" }));
    await user.type(editor.getByLabelText("Código de rol"), "docente");
    await user.type(editor.getByLabelText("Cupo diario (turnos)"), "5");
    const guardar = editor.getByRole("button", { name: "Guardar" });
    guardar.focus();
    await user.keyboard("{Enter}");

    await waitFor(() => expect(editarCupoDeRol).toHaveBeenCalledWith("docente", 5));
  });
});

describe("Los guardados se anuncian sin mover el foco (tasks.md 13.2)", () => {
  it("guardar un presupuesto anuncia por la región viva de la página, sin robar el foco", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(adminApi, "editarCupoDeRol").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montarPagina();

    const editor = within(screen.getByRole("group", { name: "Cupo diario por rol" }));
    await user.type(editor.getByLabelText("Código de rol"), "docente");
    await user.type(editor.getByLabelText("Cupo diario (turnos)"), "20");
    const guardar = editor.getByRole("button", { name: "Guardar" });
    guardar.focus();
    await user.click(guardar);

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Cupo diario por rol actualizado para docente: 20.",
    );
    expect(document.activeElement).toBe(guardar);
  });

  it("completar el toggle de mantenimiento anuncia sin mover el foco", async () => {
    vi.spyOn(adminApi, "obtenerUso").mockResolvedValue(USO_VACIO);
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(adminApi, "editarMantenimiento").mockResolvedValue({
      activo: true,
      razon: "Prueba",
    });
    const user = userEvent.setup();

    montarPagina();

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    await user.click(grupo.getByRole("switch", { name: "Asistente en mantenimiento" }));
    await user.type(grupo.getByLabelText(/Razón/), "Prueba");
    const guardar = grupo.getByRole("button", { name: "Guardar cambios" });
    guardar.focus();
    await user.click(guardar);

    expect(await screen.findByRole("status")).toHaveTextContent("Modo mantenimiento activado.");
    expect(document.activeElement).toBe(guardar);
  });
});
