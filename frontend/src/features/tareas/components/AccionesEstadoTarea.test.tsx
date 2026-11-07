import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import type { ActorTarea, Tarea } from "../types";
import { AccionesEstadoTarea } from "./AccionesEstadoTarea";

const ACTOR: ActorTarea = { nombre: "Ana", rol: "Docente" };

function tarea(extra: Partial<Tarea> = {}): Tarea {
  return {
    version: 1,
    id: "t1",
    numero: 1,
    titulo: "Tarea",
    descripcion: "",
    fechaInicio: "2026-03-01",
    fechaFin: "2026-04-01",
    prioridad: "media",
    tipo: "academica",
    estado: "en_curso",
    porcentajeAvance: 60,
    avanceCalculado: false,
    responsable: { id: "u1", nombre: "Ana", rol: "Docente" },
    creadoPor: { id: "u2", nombre: "Beto", rol: "Decanato" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
    ...extra,
  };
}

/** Monta el panel una sola vez, para conservar su estado, y devuelve cómo cambiar la tarea que recibe. */
function montar(inicial: Tarea) {
  let cambiar: (siguiente: Tarea) => void = () => {};
  function Arnes() {
    const [actual, setActual] = useState(inicial);
    cambiar = setActual;
    return (
      <AccionesEstadoTarea
        tarea={actual}
        actor={ACTOR}
        enviando={false}
        onCambiarEstado={vi.fn()}
        onEditarAvance={vi.fn()}
      />
    );
  }
  render(<RouterProvider router={createMemoryRouter([{ path: "/", element: <Arnes /> }])} />);
  return (siguiente: Tarea) => act(() => cambiar(siguiente));
}

const campoAvance = () => screen.getByRole("spinbutton", { name: "Porcentaje de avance" });

describe("AccionesEstadoTarea — avance que cambia por otra vía", () => {
  it("actualiza el campo si no había cambios sin guardar", () => {
    const cambiar = montar(tarea());

    cambiar(tarea({ porcentajeAvance: 100 }));

    expect(campoAvance()).toHaveValue(100);
  });

  it("conserva el avance y la solución que se estaban escribiendo", async () => {
    const cambiar = montar(tarea());
    await userEvent.click(screen.getByRole("button", { name: /Resuelta/ }));
    const solucion = screen.getByRole("textbox");
    await userEvent.type(solucion, "Se corrigió");
    await userEvent.clear(campoAvance());
    await userEvent.type(campoAvance(), "75");

    cambiar(tarea({ porcentajeAvance: 90 }));

    expect(screen.getByRole("textbox")).toHaveValue("Se corrigió");
    expect(campoAvance()).toHaveValue(75);
  });
});
