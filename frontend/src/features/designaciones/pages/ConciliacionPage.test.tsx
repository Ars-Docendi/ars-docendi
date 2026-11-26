import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Conciliacion, EntradaConciliacion, PedidoConciliacion } from "../api/conciliacionApi";
import { ConciliacionPage } from "./ConciliacionPage";

const api = vi.hoisted(() => ({
  consultarConciliacion: vi.fn(),
  sincronizarConciliacion: vi.fn(),
  rechazarEnConciliacion: vi.fn(),
}));
vi.mock("../api/conciliacionApi", () => api);

const valores = (cargo: string, horas: number) => ({
  cargo,
  dedicacion: "Simple",
  horas,
  horasInvestigacion: null,
  horasExternas: null,
});

function pedido(datos: Partial<PedidoConciliacion> & { id: string }): PedidoConciliacion {
  return {
    numero: `P-${datos.id}`,
    version: 1,
    novedad: "Alta",
    estado: "en_lote",
    materiaId: `m-${datos.id}`,
    materia: `Materia ${datos.id}`,
    carreraId: "c1",
    carrera: "Ingeniería en Informática",
    hoy: null,
    queda: valores("JTP", 8),
    justificacion: null,
    tipoBaja: null,
    tipoBajaDetalle: null,
    motivoRechazo: null,
    sincronizadoEn: null,
    sincronizadoPor: null,
    acciones: ["sincronizar", "rechazar", "editar"],
    ...datos,
  };
}

function entrada(personaId: string, apellido: string, p: PedidoConciliacion): EntradaConciliacion {
  return {
    personaId,
    nombre: "Ana",
    apellido,
    cuil: null,
    documento: "30111222",
    personaNueva: false,
    pedido: p,
    vigentes: [],
  };
}

function conciliacion(docentes: EntradaConciliacion[]): Conciliacion {
  return {
    periodo: {
      id: "per",
      nombre: "2026-2",
      cargaDesde: "2026-07-01",
      cargaHasta: "2026-07-31",
      impactoDesde: "2026-08-01",
      impactoHasta: "2026-12-31",
      activo: true,
    },
    docentes,
  };
}

function renderizar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <ConciliacionPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("Conciliación del lote", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.sincronizarConciliacion.mockResolvedValue({ numeros: [] });
  });

  it("sincroniza sólo los docentes aceptados, todos sus pedidos en lote con la versión vista", async () => {
    api.consultarConciliacion.mockResolvedValue(
      conciliacion([
        entrada("a", "Benítez", pedido({ id: "1", version: 3 })),
        entrada("a", "Benítez", pedido({ id: "2", version: 5, novedad: "Baja", queda: null })),
        entrada("b", "Ferreyra", pedido({ id: "3" })),
      ]),
    );
    const usuario = userEvent.setup();
    renderizar();

    await usuario.click(await screen.findByRole("checkbox", { name: /Benítez/ }));
    await usuario.click(screen.getByRole("button", { name: "Sincronizar 2 cambios" }));
    const dialogo = await screen.findByRole("dialog");
    expect(within(dialogo).queryByText(/Ferreyra/)).not.toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole("button", { name: "Sincronizar 2 cambios" }));

    await waitFor(() => expect(api.sincronizarConciliacion).toHaveBeenCalledTimes(1));
    const [pedidos, clave] = api.sincronizarConciliacion.mock.calls[0];
    expect(pedidos).toEqual([
      { id: "1", version: 3 },
      { id: "2", version: 5 },
    ]);
    expect(clave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("rechazar un pedido de un docente aceptado lo deja sin aceptar", async () => {
    const segundo = pedido({ id: "2", materia: "Física I" });
    api.consultarConciliacion.mockResolvedValue(
      conciliacion([
        entrada("a", "Benítez", pedido({ id: "1", materia: "Álgebra" })),
        entrada("a", "Benítez", segundo),
      ]),
    );
    const usuario = userEvent.setup();
    renderizar();

    await usuario.click(await screen.findByRole("checkbox", { name: /Benítez/ }));
    expect(screen.getByRole("button", { name: "Sincronizar 2 cambios" })).toBeEnabled();

    api.rechazarEnConciliacion.mockResolvedValue(undefined);
    api.consultarConciliacion.mockResolvedValue(
      conciliacion([
        entrada(
          "a",
          "Benítez",
          pedido({
            id: "1",
            materia: "Álgebra",
            estado: "no_aprobado",
            version: 2,
            acciones: ["editar"],
          }),
        ),
        entrada("a", "Benítez", segundo),
      ]),
    );
    await usuario.click(screen.getByRole("button", { name: /Ver el detalle de Benítez/ }));
    const filaAlgebra = screen.getAllByRole("row", { name: /Álgebra/ }).at(-1)!;
    await usuario.click(within(filaAlgebra).getByRole("button", { name: "Rechazar" }));
    const dialogo = await screen.findByRole("dialog");
    await usuario.type(within(dialogo).getByRole("textbox"), "No lo aprobó Rectorado");
    await usuario.click(within(dialogo).getByRole("button", { name: "Rechazar" }));

    expect(api.rechazarEnConciliacion).toHaveBeenCalledWith("1", "No lo aprobó Rectorado");
    await waitFor(() => expect(screen.getByRole("button", { name: "Sincronizar" })).toBeDisabled());
    expect(screen.getByRole("checkbox", { name: /Benítez/ })).not.toBeChecked();
  });

  it("no deja rechazar sin motivo", async () => {
    api.consultarConciliacion.mockResolvedValue(
      conciliacion([entrada("a", "Benítez", pedido({ id: "1" }))]),
    );
    const usuario = userEvent.setup();
    renderizar();

    await usuario.click(await screen.findByRole("button", { name: /Ver el detalle de Benítez/ }));
    await usuario.click(screen.getByRole("button", { name: "Rechazar" }));
    const dialogo = await screen.findByRole("dialog");
    await usuario.click(within(dialogo).getByRole("button", { name: "Rechazar" }));

    expect(within(dialogo).getByText("Escribí el motivo del rechazo.")).toBeInTheDocument();
    expect(api.rechazarEnConciliacion).not.toHaveBeenCalled();
  });

  it("sin pedidos en lote explica que la pantalla muestra únicamente los del período activo", async () => {
    api.consultarConciliacion.mockResolvedValue(conciliacion([]));
    renderizar();

    expect(await screen.findByText("No hay docentes por conciliar")).toBeInTheDocument();
    expect(
      screen.getByText(/únicamente los pedidos en lote del período activo/),
    ).toBeInTheDocument();
  });
});
