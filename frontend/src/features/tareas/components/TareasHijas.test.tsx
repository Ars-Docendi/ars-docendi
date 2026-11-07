import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Tarea } from "../types";
import { TareasHijas } from "./TareasHijas";

const vincular = vi.fn();
const desvincular = vi.fn();

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      id: "u2",
      name: "Beto",
      role: "Decanato",
      permissions: ["tareas.ver", "tareas.gestionar"],
    },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));
vi.mock("../hooks/useAccionesTarea", () => ({
  useVincularHija: () => ({ mutate: vincular, isPending: false, isError: false, error: null }),
  useDesvincularHija: () => ({
    mutate: desvincular,
    isPending: false,
    isError: false,
    error: null,
  }),
}));

function tarea(id: string, numero: number, extra: Partial<Tarea> = {}): Tarea {
  return {
    version: numero * 10,
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

function montar(todas: Tarea[], puedeVincular = true) {
  render(
    <MemoryRouter>
      <TareasHijas
        tarea={todas[0]}
        todas={todas}
        puedeCrear
        puedeVincular={puedeVincular}
        onCrearHija={() => {}}
      />
    </MemoryRouter>,
  );
}

async function elegir(nombre: string) {
  await userEvent.click(screen.getByRole("combobox", { name: "Vincular una tarea existente" }));
  await userEvent.click(screen.getByRole("option", { name: nombre }));
}

beforeEach(() => vi.clearAllMocks());

describe("TareasHijas — vincular una tarea existente", () => {
  it("con el mismo proyecto vincula directamente", async () => {
    montar([tarea("p", 1), tarea("libre", 2)]);

    await elegir("N° 2 — Tarea 2");

    expect(vincular).toHaveBeenCalledWith({ id: "p", hijaId: "libre", version: 20 });
  });

  it("con otro proyecto pide confirmar antes de vincular", async () => {
    montar([tarea("p", 1, { proyectoId: "q" }), tarea("libre", 2)]);

    await elegir("N° 2 — Tarea 2");

    expect(vincular).not.toHaveBeenCalled();
    const dialogo = screen.getByRole("dialog");
    await userEvent.click(within(dialogo).getByRole("button", { name: "Vincular" }));
    expect(vincular).toHaveBeenCalledWith({ id: "p", hijaId: "libre", version: 20 });
  });

  it("desvincula una hija con su versión", async () => {
    montar([tarea("p", 1), tarea("h", 2, { tareaPadreId: "p" })]);

    await userEvent.click(screen.getByRole("button", { name: 'Desvincular "Tarea 2"' }));

    expect(desvincular).toHaveBeenCalledWith({ id: "p", hijaId: "h", version: 20 });
  });

  it("sin permiso no ofrece vincular ni desvincular", () => {
    montar([tarea("p", 1), tarea("h", 2, { tareaPadreId: "p" }), tarea("libre", 3)], false);

    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Desvincular/ })).not.toBeInTheDocument();
  });
});
