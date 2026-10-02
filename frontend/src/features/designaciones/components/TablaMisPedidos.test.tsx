import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { PedidoDesignacion } from "../types";
import { TablaMisPedidos } from "./TablaMisPedidos";
import { FILTROS_INICIALES } from "./filtrosMisPedidos";

function pedido(): PedidoDesignacion {
  return {
    id: "p1",
    numero: "N°-2026-0001",
    periodoId: "periodo-1",
    catedra: "Cálculo I",
    carrera: "Ingeniería en Informática",
    docente: { dni: "1", nombre: "Ana García", legajo: "1005", antiguedad: 3 },
    horas: 6,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Alta",
    horasExternas: 0,
    horasInvestigacion: 0,
    adjuntos: [],
    estado: "borrador",
    prioritario: false,
    historial: [],
    accionesPermitidas: ["editar", "eliminar"],
  };
}

describe("TablaMisPedidos", () => {
  it("usa roles nativos y conserva la navegación de fila y acciones", async () => {
    const user = userEvent.setup();
    const onVer = vi.fn();
    const onEditar = vi.fn();
    const onEliminar = vi.fn();
    render(
      <TablaMisPedidos
        pedidos={[pedido()]}
        onVerDetalle={onVer}
        onEditar={onEditar}
        onEliminar={onEliminar}
      />,
    );

    expect(screen.getByRole("table", { name: "Mis pedidos de designación" })).toBeInTheDocument();
    const fila = screen.getByRole("row", { name: /Ver el pedido de Ana García/ });
    await user.click(fila);
    expect(onVer).toHaveBeenCalledOnce();

    await user.click(within(fila).getByRole("button", { name: "Editar" }));
    expect(onEditar).toHaveBeenCalledOnce();
    await user.click(within(fila).getByRole("button", { name: "Eliminar pedido de Ana García" }));
    expect(onEliminar).toHaveBeenCalledOnce();
  });

  it("abre filtros de encabezado sin ordenar y notifica su cambio", async () => {
    const user = userEvent.setup();
    const onFiltrosChange = vi.fn();
    const onOrdenChange = vi.fn();
    render(
      <TablaMisPedidos
        pedidos={[pedido()]}
        filtros={FILTROS_INICIALES}
        onFiltrosChange={onFiltrosChange}
        onOrdenChange={onOrdenChange}
        onVerDetalle={vi.fn()}
        onEditar={vi.fn()}
        onEliminar={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Filtrar Docente" }));
    expect(screen.getByRole("dialog", { name: "Filtro de Docente" })).toBeInTheDocument();
    await user.type(screen.getByRole("textbox", { name: "Buscar Docente" }), "ana");
    expect(onFiltrosChange).toHaveBeenCalled();
    expect(onOrdenChange).not.toHaveBeenCalled();

    await user.click(screen.getByRole("columnheader", { name: "Docente" }));
    expect(onOrdenChange).toHaveBeenCalledWith({ columna: "docente", direccion: "asc" });
  });

  it("activa la fila con Enter y Espacio", async () => {
    const user = userEvent.setup();
    const onVer = vi.fn();
    render(
      <TablaMisPedidos
        pedidos={[pedido()]}
        onVerDetalle={onVer}
        onEditar={vi.fn()}
        onEliminar={vi.fn()}
      />,
    );
    const fila = screen.getByRole("row", { name: /Ver el pedido de Ana García/ });
    fila.focus();
    await user.keyboard("{Enter}");
    await user.keyboard(" ");
    expect(onVer).toHaveBeenCalledTimes(2);
  });
});
