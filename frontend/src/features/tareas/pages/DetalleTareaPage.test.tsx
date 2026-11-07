import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { Tarea } from "../types";
import { DetalleTareaPage } from "./DetalleTareaPage";

function tarea(id: string, numero: number, extra: Partial<Tarea> = {}): Tarea {
  return {
    version: 1,
    id,
    numero,
    titulo: `Tarea ${numero}`,
    descripcion: "",
    fechaInicio: "2026-03-01",
    fechaFin: "2026-04-01",
    prioridad: "media",
    tipo: "academica",
    estado: "pendiente",
    porcentajeAvance: 0,

    avanceCalculado: false,
    responsable: { id: "u1", nombre: "Ana", rol: "Docente" },
    creadoPor: { id: "u2", nombre: "Beto", rol: "Decanato" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
    ...extra,
  };
}

const TAREAS = [
  tarea("t1", 1),
  tarea("t2", 2, { tareaPadreId: "t1" }),
  tarea("t3", 3, { fechaInicio: "2026-03-01", fechaFin: "2026-03-20" }),
];

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: { id: "u1", name: "Ana", role: "Docente", permissions: ["tareas.ver"] },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));
vi.mock("../hooks/useTareas", () => ({
  useTarea: (id: string) => ({
    data: TAREAS.find((t) => t.id === id),
    isLoading: false,
    isError: false,
  }),
  useListadoTareas: () => ({ data: TAREAS }),
}));
vi.mock("../hooks/useProyectos", () => ({ useListadoProyectos: () => ({ data: [] }) }));
vi.mock("../hooks/useAccionesTarea", () => {
  const mutacion = () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null });
  return {
    useAgregarComentario: mutacion,
    useAgregarRelacion: mutacion,
    useCambiarEstadoTarea: mutacion,
    useCrearTarea: mutacion,
    useEditarAvance: mutacion,
    useEditarTarea: mutacion,
    useQuitarRelacion: mutacion,
    useVincularHija: mutacion,
    useDesvincularHija: mutacion,
  };
});

function montar(entradas: Parameters<typeof createMemoryRouter>[1]) {
  const router = createMemoryRouter(
    [
      { path: "/tareas", element: <p>Listado de tareas</p> },
      { path: "/tareas/:id", element: <DetalleTareaPage /> },
    ],
    entradas,
  );
  render(<RouterProvider router={router} />);
  return router;
}

beforeEach(() => vi.clearAllMocks());

describe("DetalleTareaPage — Vencimiento", () => {
  afterEach(() => vi.useRealTimers());

  it("una tarea que vence hoy no figura como vencida", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 2, 20, 22, 0));

    montar({ initialEntries: ["/tareas/t3"] });

    expect(screen.getByRole("img", { name: "Próximo a vencer" })).toBeInTheDocument();
    expect(screen.queryByText("Vencido")).not.toBeInTheDocument();
  });
});

describe("DetalleTareaPage — Volver", () => {
  it("con un link directo vuelve al listado sin salir de la app", async () => {
    // Una sola entrada de historial: `navigate(-1)` no tiene a dónde ir.
    const router = montar({ initialEntries: ["/tareas/t1"] });

    await userEvent.click(screen.getByRole("link", { name: "Volver a Tareas" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/tareas"));
  });

  it("vuelve a la lista de origen con sus filtros, también después de abrir una tarea hija", async () => {
    const router = montar({
      initialEntries: [
        "/tareas?estado=todas",
        { pathname: "/tareas/t1", state: { volverA: "/tareas?estado=todas" } },
      ],
      initialIndex: 1,
    });

    await userEvent.click(screen.getByRole("link", { name: /Tarea 2/ }));
    expect(await screen.findByRole("heading", { name: "Tarea 2" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("link", { name: "Volver a Tareas" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/tareas"));
    expect(router.state.location.search).toBe("?estado=todas");
  });
});
