import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { TablaDeResultado } from "./components/TablaDeResultado";
import { montar } from "./test/soporte";
import type { ColumnaDelResultado } from "./types";

// ============================================================
// Adjustable column widths on a phone (listas-movil): the product owner could not read a
// result table on a phone without widening its columns. jsdom has no layout, so every header
// cell is measured as 120 px wide.
// ============================================================

const COLUMNAS: ColumnaDelResultado[] = [
  { nombre: "carrera", sensible: false },
  { nombre: "departamento", sensible: false },
];

const FILAS: unknown[][] = [
  ["Carrera de ejemplo 2", "Departamento de Ingeniería"],
  ["Carrera de ejemplo 1", "Departamento de Humanidades"],
];

function encabezado(nombre: string, ambito: HTMLElement = document.body): HTMLElement {
  return within(ambito).getByRole("columnheader", { name: new RegExp(`^${nombre}`, "i") });
}

function asa(nombre: string, ambito: HTMLElement = document.body): HTMLElement {
  return within(ambito).getByRole("button", { name: `Ajustar ancho de «${nombre}»` });
}

beforeEach(() => {
  vi.stubGlobal("matchMedia", (consulta: string) => ({
    matches: consulta.includes("559px"),
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
  vi.spyOn(HTMLTableCellElement.prototype, "getBoundingClientRect").mockReturnValue({
    width: 120,
  } as DOMRect);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("Column widths on a phone", () => {
  it("the arrow keys on a handle change the width and a double click restores it", async () => {
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    asa("carrera").focus();
    await user.keyboard("{ArrowRight}{ArrowRight}");
    expect(encabezado("carrera").style.width).toBe("152px");
    expect(encabezado("departamento").style.width).toBe("120px");

    await user.keyboard("{ArrowLeft}".repeat(10));
    expect(encabezado("carrera").style.width).toBe("64px");

    await user.dblClick(asa("carrera"));
    expect(encabezado("carrera").style.width).toBe("120px");
  });

  it("dragging a handle widens the column without sorting, and the expanded view keeps it", async () => {
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    const handle = asa("departamento");
    fireEvent.pointerDown(handle, { clientX: 200, pointerId: 1 });
    fireEvent.pointerMove(handle, { clientX: 260, pointerId: 1 });
    fireEvent.pointerUp(handle, { clientX: 260, pointerId: 1 });
    fireEvent.click(handle);

    expect(encabezado("departamento").style.width).toBe("180px");
    expect(encabezado("departamento")).not.toHaveAttribute("aria-sort");
    expect(screen.queryByText(/Tabla ordenada por/)).toBeNull();

    await user.click(screen.getByRole("button", { name: "Ampliar tabla" }));
    const ampliada = screen.getByRole("region", { name: "Tabla ampliada" });
    expect(encabezado("departamento", ampliada).style.width).toBe("180px");
  });
});
