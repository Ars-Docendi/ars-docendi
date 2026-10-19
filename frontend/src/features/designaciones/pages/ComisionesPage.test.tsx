import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  importarComisiones,
  listarComisiones,
  listarPeriodosGuarani,
  listarPeriodosImportados,
  type Comision,
  type PeriodoImportado,
} from "../api/comisionesApi";
import { ComisionesPage } from "./ComisionesPage";

const estado = vi.hoisted(() => ({ permisos: [] as string[] }));

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      id: "u1",
      name: "Fernández, Lucía",
      initials: "LF",
      upn: "secretaria@unlam.edu.ar",
      role: "Secretaría Académica",
      roleCode: "secretaria",
      permissions: estado.permisos,
    },
  }),
}));

vi.mock("../api/comisionesApi", () => ({
  listarPeriodosImportados: vi.fn(),
  listarComisiones: vi.fn(),
  listarPeriodosGuarani: vi.fn(),
  importarComisiones: vi.fn(),
}));

const exitosa = {
  fecha: "2026-10-15T13:30:00Z",
  estado: "exitosa" as const,
  causa: null,
  usuario: "Lucía Fernández",
};

const periodos: PeriodoImportado[] = [
  {
    anioAcademico: 2026,
    id: "152",
    nombre: "2do Cuatrimestre",
    ultimaExitosa: exitosa,
    ultimoIntento: exitosa,
  },
  {
    anioAcademico: 2026,
    id: "151",
    nombre: "1er Cuatrimestre 2026",
    ultimaExitosa: exitosa,
    ultimoIntento: exitosa,
  },
];

const comisiones: Comision[] = [
  {
    id: "9001",
    nombre: "01-1234",
    materia: { codigo: "03500", nombre: "Ingeniería de Software", id: null },
    carreras: [
      { codigo: "INF", nombre: "Ingeniería en Informática", id: null },
      { codigo: "IND", nombre: "Ingeniería Industrial", id: null },
    ],
    docentes: [
      { nombre: "PÉREZ JUAN", responsabilidad: "Titular", personaId: null },
      { nombre: "GÓMEZ ANA", responsabilidad: null, personaId: null },
    ],
    horarios: [
      { asignacionId: "1", dia: 1, desde: "08:00", hasta: "12:00", aula: "Aula 101" },
      { asignacionId: "2", dia: 4, desde: "08:00", hasta: "10:00", aula: null },
    ],
    modalidadCodigo: "A",
    modalidad: "Presencial",
    ubicacion: null,
  },
];

function renderPagina() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter>
        <ComisionesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function problema(status: number, detail: string) {
  return new AxiosError("error", String(status), undefined, undefined, {
    status,
    statusText: "",
    headers: {},
    config: { headers: new AxiosHeaders() },
    data: { type: "https://ars-docendi.unlam.edu.ar/errors/x", title: detail, detail },
  });
}

beforeEach(() => {
  estado.permisos = ["comisiones.ver"];
  vi.mocked(listarPeriodosImportados).mockReset().mockResolvedValue(periodos);
  vi.mocked(listarComisiones).mockReset().mockResolvedValue(comisiones);
  vi.mocked(listarPeriodosGuarani)
    .mockReset()
    .mockResolvedValue([
      { id: "152", nombre: "2do Cuatrimestre", anio: 2026 },
      { id: "151", nombre: "1er Cuatrimestre 2026", anio: 2026 },
      { id: "140", nombre: "2do Cuatrimestre", anio: 2025 },
    ]);
  vi.mocked(importarComisiones).mockReset();
});

describe("Comisiones", () => {
  it("selecciona el período más reciente y muestra la grilla con sus columnas", async () => {
    renderPagina();

    const fila = await screen.findByRole("row", { name: /01-1234/ });
    expect(listarComisiones).toHaveBeenCalledWith("152");
    expect(within(fila).getByText("Ingeniería de Software (03500)")).toBeInTheDocument();
    expect(within(fila).getByText("Lunes 08:00–12:00 · Aula 101")).toBeInTheDocument();
    expect(within(fila).getByText("Jueves 08:00–10:00")).toBeInTheDocument();
    expect(within(fila).getByText("PÉREZ JUAN (Titular)")).toBeInTheDocument();
    expect(within(fila).getByText("Presencial")).toBeInTheDocument();
    expect(within(fila).getAllByText("—")).toHaveLength(1);
    expect(
      screen.getByText(/Actualizado el 15\/10\/2026 .* por Lucía Fernández/),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Actualizar desde Guaraní/ }),
    ).not.toBeInTheDocument();
    expect(listarPeriodosGuarani).not.toHaveBeenCalled();
  });

  it("muestra el año del período sólo si el nombre no lo incluye", async () => {
    renderPagina();

    const selector = await screen.findByRole("combobox", { name: "Período lectivo" });
    expect(
      within(selector).getByRole("option", { name: "2do Cuatrimestre 2026" }),
    ).toBeInTheDocument();
    expect(
      within(selector).getByRole("option", { name: "1er Cuatrimestre 2026" }),
    ).toBeInTheDocument();
  });

  it("filtra por docente desde el encabezado", async () => {
    vi.mocked(listarComisiones).mockResolvedValue([
      ...comisiones,
      { ...comisiones[0], id: "9002", nombre: "01-5678", docentes: [] },
    ]);
    renderPagina();
    await screen.findByRole("row", { name: /01-5678/ });

    await userEvent.click(screen.getByRole("button", { name: /Filtrar Docentes/i }));
    await userEvent.type(screen.getByRole("textbox", { name: "Buscar Docentes" }), "perez");

    expect(screen.queryByRole("row", { name: /01-5678/ })).not.toBeInTheDocument();
    expect(screen.getByRole("row", { name: /01-1234/ })).toBeInTheDocument();
  });

  it("avisa cuando el último intento falló y conserva la grilla", async () => {
    vi.mocked(listarPeriodosImportados).mockResolvedValue([
      {
        ...periodos[0],
        ultimoIntento: {
          ...exitosa,
          estado: "fallida",
          causa: "timeout",
          fecha: "2026-10-16T10:00:00Z",
        },
      },
    ]);
    renderPagina();

    expect(await screen.findByText("El último intento de actualización falló")).toBeInTheDocument();
    expect(await screen.findByRole("row", { name: /01-1234/ })).toBeInTheDocument();
  });

  it("sin importaciones explica el estado vacío e invita a importar a quien puede", async () => {
    estado.permisos = ["comisiones.ver", "comisiones.importar"];
    vi.mocked(listarPeriodosImportados).mockResolvedValue([]);
    renderPagina();

    expect(await screen.findByText(/Todavía no hay comisiones importadas/)).toHaveTextContent(
      "Actualizar desde Guaraní",
    );
    expect(listarComisiones).not.toHaveBeenCalled();
  });

  it("importa un período elegido en el modal y lo selecciona", async () => {
    estado.permisos = ["comisiones.ver", "comisiones.importar"];
    vi.mocked(importarComisiones).mockResolvedValue({
      id: "i1",
      anioAcademico: 2026,
      periodoLectivoId: "151",
      periodoLectivoNombre: "1er Cuatrimestre 2026",
      cantidad: 120,
      docentesSinVincular: 3,
      carrerasSinVincular: 0,
    });
    renderPagina();

    await userEvent.click(await screen.findByRole("button", { name: "Actualizar desde Guaraní" }));
    const dialogo = await screen.findByRole("dialog");
    const importar = within(dialogo).getByRole("button", { name: "Importar" });
    await within(dialogo).findByRole("option", { name: "1er Cuatrimestre 2026" });
    expect(importar).toBeDisabled();
    expect(within(dialogo).queryByRole("option", { name: "2025" })).toBeInTheDocument();

    await userEvent.selectOptions(
      within(dialogo).getByRole("combobox", { name: "Período lectivo" }),
      "151",
    );
    await userEvent.click(importar);

    await waitFor(() => expect(importarComisiones).toHaveBeenCalledWith("151"));
    expect(await screen.findByText(/Se importaron 120 comisiones/)).toHaveTextContent(
      "3 docente(s) no figuran en el sistema.",
    );
    await waitFor(() => expect(listarComisiones).toHaveBeenLastCalledWith("151"));
  });

  it("muestra el error de la importación sin cerrar el modal", async () => {
    estado.permisos = ["comisiones.ver", "comisiones.importar"];
    vi.mocked(importarComisiones).mockRejectedValue(
      problema(409, "Ya hay una importación en curso para este período. Esperá a que termine."),
    );
    renderPagina();

    await userEvent.click(await screen.findByRole("button", { name: "Actualizar desde Guaraní" }));
    const dialogo = await screen.findByRole("dialog");
    await within(dialogo).findByRole("option", { name: "2do Cuatrimestre" });
    await userEvent.selectOptions(
      within(dialogo).getByRole("combobox", { name: "Período lectivo" }),
      "152",
    );
    await userEvent.click(within(dialogo).getByRole("button", { name: "Importar" }));

    expect(await within(dialogo).findByText(/Ya hay una importación en curso/)).toBeInTheDocument();
  });

  it("informa cuando Guaraní no está configurado", async () => {
    estado.permisos = ["comisiones.ver", "comisiones.importar"];
    vi.mocked(listarPeriodosGuarani).mockRejectedValue(
      problema(503, "La integración con Guaraní no está configurada en este ambiente."),
    );
    renderPagina();

    await userEvent.click(await screen.findByRole("button", { name: "Actualizar desde Guaraní" }));

    expect(await screen.findByText(/no está configurada en este ambiente/)).toBeInTheDocument();
    expect(
      within(screen.getByRole("dialog")).getByRole("button", { name: "Importar" }),
    ).toBeDisabled();
  });
});
