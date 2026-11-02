import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import type { SolicitudReservaAula } from "../types";
import { TablaMisSolicitudes } from "./TablaMisSolicitudes";
import { TablaTodasLasSolicitudes } from "./TablaTodasLasSolicitudes";

function solicitud(id: string, estado: SolicitudReservaAula["estado"]): SolicitudReservaAula {
  return {
    id,
    dia: "2026-11-20",
    horarioDesde: "08:00:00",
    horarioHasta: "10:00:00",
    cantidadAlumnosAprox: 30,
    materia: { id: "m1", codigo: "3001", nombre: "Ingeniería de Software" },
    comision: "K3001",
    estado,
    creadoEn: "2026-10-01T10:00:00Z",
    docente: { id: "d1", nombre: "Carla", apellido: "López" },
    esPropia: true,
  };
}

const TODOS_LOS_ESTADOS = (["pendiente", "aprobada", "rechazada", "cancelada"] as const).map(
  (estado) => solicitud(`s-${estado}`, estado),
);

describe.each([
  [
    "Mis solicitudes",
    (onAbrir: () => void) => (
      <TablaMisSolicitudes
        solicitudes={TODOS_LOS_ESTADOS}
        onCancelar={() => {}}
        onAbrir={onAbrir}
      />
    ),
  ],
  [
    "Todas las solicitudes",
    (onAbrir: () => void) => (
      <TablaTodasLasSolicitudes solicitudes={TODOS_LOS_ESTADOS} onAbrir={onAbrir} />
    ),
  ],
])("%s", (_nombre, tabla) => {
  it("opens the detail with a single click on any row, whatever its state", async () => {
    const usuario = userEvent.setup();
    const onAbrir = vi.fn();
    render(<MemoryRouter>{tabla(onAbrir)}</MemoryRouter>);

    for (const fila of screen.getAllByRole("row").slice(1)) {
      await usuario.click(fila.querySelectorAll("td")[3]);
    }

    expect(onAbrir.mock.calls.map(([abierta]) => abierta.estado)).toEqual([
      "pendiente",
      "aprobada",
      "rechazada",
      "cancelada",
    ]);
  });

  it("has a real link to the detail in the primary cell", () => {
    render(<MemoryRouter>{tabla(vi.fn())}</MemoryRouter>);

    const enlaces = screen.getAllByRole("link", { name: /^Ver la solicitud/ });
    expect(enlaces.map((e) => e.getAttribute("href"))).toEqual([
      "/aulas/solicitudes/s-pendiente",
      "/aulas/solicitudes/s-aprobada",
      "/aulas/solicitudes/s-rechazada",
      "/aulas/solicitudes/s-cancelada",
    ]);
  });

  it("opens the detail with Enter on the focused row", async () => {
    const usuario = userEvent.setup();
    const onAbrir = vi.fn();
    render(<MemoryRouter>{tabla(onAbrir)}</MemoryRouter>);

    screen.getAllByRole("row")[2].focus();
    await usuario.keyboard("{Enter}");

    expect(onAbrir).toHaveBeenCalledWith(expect.objectContaining({ estado: "aprobada" }));
  });
});

describe("Mis solicitudes — Cancelar", () => {
  it("keeps the row Cancelar button, which cancels without opening the detail", async () => {
    const usuario = userEvent.setup();
    const onAbrir = vi.fn();
    const onCancelar = vi.fn();
    render(
      <MemoryRouter>
        <TablaMisSolicitudes
          solicitudes={TODOS_LOS_ESTADOS}
          onCancelar={onCancelar}
          onAbrir={onAbrir}
        />
      </MemoryRouter>,
    );

    const botones = screen.getAllByRole("button", { name: "Cancelar" });
    expect(botones.map((b) => (b as HTMLButtonElement).disabled)).toEqual([
      false,
      true,
      true,
      true,
    ]);
    await usuario.click(botones[0]);

    expect(onCancelar).toHaveBeenCalledWith(expect.objectContaining({ id: "s-pendiente" }));
    expect(onAbrir).not.toHaveBeenCalled();
  });
});
