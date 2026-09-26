import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router-dom";

import { routes } from "./routes";
import { SoporteHistorialPage } from "./pages/SoporteHistorialPage";
import * as soporteApi from "./api/soporteHistorialApi";
import type { PersonaParaSoporte } from "./api/soporteHistorialApi";
import type { ConversacionDetalle } from "./types";
import * as useCurrentUserMod from "../../shared/auth/useCurrentUser";

// ============================================================
// La pantalla de lectura de soporte del historial ajeno
// (asistente-acceso-de-soporte-al-historial, tasks.md §13).
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

function montarPagina(nodo: React.ReactNode) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(<QueryClientProvider client={cliente}>{nodo}</QueryClientProvider>);
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

describe("La ruta, sin el permiso (tasks.md 13.1)", () => {
  it("no hay match de ruta: navegar a la URL no muestra la pantalla", async () => {
    conUsuario([]);

    montarConRouter("/asistente/soporte-historial");

    await waitFor(() =>
      expect(screen.queryByText("Historial del asistente (soporte)")).not.toBeInTheDocument(),
    );
  });
});

describe("La ruta, con el permiso (tasks.md 13.1)", () => {
  it("la pantalla se monta", async () => {
    conUsuario(["asistente.leer_historial_ajeno"]);
    vi.spyOn(soporteApi, "buscarPersonasParaSoporte").mockResolvedValue([]);

    montarConRouter("/asistente/soporte-historial");

    expect(await screen.findByText("Historial del asistente (soporte)")).toBeInTheDocument();
  });
});

const PERSONA: PersonaParaSoporte = {
  id: "66666666-6666-4666-8666-666666666666",
  nombre: "Marina",
  apellido: "Díaz",
  documento: "31089234",
};

const CONVERSACION_AJENA: ConversacionDetalle = {
  id: "77777777-7777-4777-8777-777777777777",
  titulo: "¿Cuántas materias tengo?",
  creadoEn: "2026-03-01T09:00:00Z",
  ultimaActividad: "2026-03-01T09:05:00Z",
  archivada: false,
  turnos: [
    {
      id: "88888888-8888-4888-8888-888888888888",
      pregunta: "¿Cuántas materias tengo?",
      sql: "SELECT count(*) FROM designaciones.designaciones",
      estado: "respondida",
      ocurrioEn: "2026-03-01T09:00:05Z",
    },
  ],
};

describe("La razón es obligatoria (tasks.md 13.2)", () => {
  it("sin razón, no se pide ni el listado ni una conversación puntual", async () => {
    vi.spyOn(soporteApi, "buscarPersonasParaSoporte").mockResolvedValue([]);
    const listar = vi.spyOn(soporteApi, "listarHistorialDeSoporte").mockResolvedValue([]);

    montarPagina(<SoporteHistorialPage />);

    expect(await screen.findByLabelText("Razón del acceso")).toBeInTheDocument();
    expect(listar).not.toHaveBeenCalled();
  });

  it("elegir a la persona sin razón no muestra su historial; con razón, sí lo pide", async () => {
    vi.spyOn(soporteApi, "buscarPersonasParaSoporte").mockResolvedValue([PERSONA]);
    const listar = vi.spyOn(soporteApi, "listarHistorialDeSoporte").mockResolvedValue([
      {
        id: CONVERSACION_AJENA.id,
        titulo: CONVERSACION_AJENA.titulo,
        creadoEn: CONVERSACION_AJENA.creadoEn,
        ultimaActividad: CONVERSACION_AJENA.ultimaActividad,
        archivada: false,
      },
    ]);
    const user = userEvent.setup();

    montarPagina(<SoporteHistorialPage />);

    await user.click(await screen.findByRole("button", { name: /Díaz, Marina/ }));
    expect(listar).not.toHaveBeenCalled();
    expect(
      screen.getByText("Ingresá una razón para listar o abrir el historial de esta persona."),
    ).toBeInTheDocument();

    await user.type(
      screen.getByLabelText("Razón del acceso"),
      "El usuario reportó una respuesta incorrecta",
    );

    await waitFor(() =>
      expect(listar).toHaveBeenCalledWith(
        PERSONA.id,
        "El usuario reportó una respuesta incorrecta",
      ),
    );
    expect(await screen.findByText(CONVERSACION_AJENA.titulo)).toBeInTheDocument();
  });
});

describe("Sólo pregunta/SQL/desenlace/momentos, nada de filas ni re-ejecución (tasks.md 13.3)", () => {
  it("abrir una conversación no muestra filas de resultado ni ningún control de re-ejecución", async () => {
    vi.spyOn(soporteApi, "buscarPersonasParaSoporte").mockResolvedValue([PERSONA]);
    vi.spyOn(soporteApi, "listarHistorialDeSoporte").mockResolvedValue([
      {
        id: CONVERSACION_AJENA.id,
        titulo: CONVERSACION_AJENA.titulo,
        creadoEn: CONVERSACION_AJENA.creadoEn,
        ultimaActividad: CONVERSACION_AJENA.ultimaActividad,
        archivada: false,
      },
    ]);
    vi.spyOn(soporteApi, "leerHistorialDeSoporte").mockResolvedValue(CONVERSACION_AJENA);
    const user = userEvent.setup();

    montarPagina(<SoporteHistorialPage />);

    await user.click(await screen.findByRole("button", { name: /Díaz, Marina/ }));
    await user.type(screen.getByLabelText("Razón del acceso"), "Reviso un reclamo");
    await user.click(await screen.findByRole("button", { name: CONVERSACION_AJENA.titulo }));

    const detalle = await screen.findByLabelText(
      `Conversación de soporte: ${CONVERSACION_AJENA.titulo}`,
    );

    // Lo que SÍ tiene que verse: pregunta, SQL (a pedido), desenlace, momento.
    expect(detalle.querySelector(".adoc-asistente-soporte-turno-pregunta")).toHaveTextContent(
      CONVERSACION_AJENA.turnos[0].pregunta,
    );
    expect(within(detalle).getByText(/Respondida/)).toBeInTheDocument();
    await user.click(within(detalle).getByText("Ver la consulta"));
    expect(within(detalle).getByText(CONVERSACION_AJENA.turnos[0].sql!)).toBeInTheDocument();

    // Lo que NUNCA tiene que verse: una tabla de filas, o cualquier acción de
    // re-ejecución.
    expect(screen.queryByRole("table")).toBeNull();
    expect(screen.queryByRole("button", { name: "Volver a consultar" })).toBeNull();
  });
});

describe("Marcas «Archivada» / «Pendiente de borrado» (tasks.md 3.5)", () => {
  it("marca una conversación archivada", async () => {
    vi.spyOn(soporteApi, "buscarPersonasParaSoporte").mockResolvedValue([PERSONA]);
    vi.spyOn(soporteApi, "listarHistorialDeSoporte").mockResolvedValue([
      {
        id: CONVERSACION_AJENA.id,
        titulo: CONVERSACION_AJENA.titulo,
        creadoEn: CONVERSACION_AJENA.creadoEn,
        ultimaActividad: CONVERSACION_AJENA.ultimaActividad,
        archivada: true,
      },
    ]);
    const user = userEvent.setup();

    montarPagina(<SoporteHistorialPage />);

    await user.click(await screen.findByRole("button", { name: /Díaz, Marina/ }));
    await user.type(screen.getByLabelText("Razón del acceso"), "Reviso un reclamo");

    expect(await screen.findByText("Archivada")).toBeInTheDocument();
    expect(screen.queryByText("Pendiente de borrado")).toBeNull();
  });

  it("marca una conversación pendiente de borrado", async () => {
    vi.spyOn(soporteApi, "buscarPersonasParaSoporte").mockResolvedValue([PERSONA]);
    vi.spyOn(soporteApi, "listarHistorialDeSoporte").mockResolvedValue([
      {
        id: CONVERSACION_AJENA.id,
        titulo: CONVERSACION_AJENA.titulo,
        creadoEn: CONVERSACION_AJENA.creadoEn,
        ultimaActividad: CONVERSACION_AJENA.ultimaActividad,
        archivada: false,
        pendienteDeBorrado: true,
      },
    ]);
    const user = userEvent.setup();

    montarPagina(<SoporteHistorialPage />);

    await user.click(await screen.findByRole("button", { name: /Díaz, Marina/ }));
    await user.type(screen.getByLabelText("Razón del acceso"), "Reviso un reclamo");

    expect(await screen.findByText("Pendiente de borrado")).toBeInTheDocument();
    expect(screen.queryByText("Archivada")).toBeNull();
  });
});
