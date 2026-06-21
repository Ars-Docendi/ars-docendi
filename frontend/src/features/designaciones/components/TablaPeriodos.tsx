import { useState } from "react";
import { Table, Input, Select } from "@ars-docendi/ui";
import type { EstadoPeriodo, PeriodoDesignacion } from "../types";
import { EstadoPeriodoPill } from "./EstadoPeriodoPill";
import { MenuAccionesPeriodo } from "./MenuAccionesPeriodo";

interface TablaPeriodosProps {
  periodos: PeriodoDesignacion[];
  onEditar: (periodo: PeriodoDesignacion) => void;
  onEliminar: (periodo: PeriodoDesignacion) => void;
}

type FiltroEstado = EstadoPeriodo | "todos";

const ORDEN_CUATRIMESTRE: Record<string, number> = { "2C": 2, "1C": 1, Verano: 0 };

function formatearFecha(fechaIso: string): string {
  const [anio, mes, dia] = fechaIso.split("-");
  return `${dia}/${mes}/${anio}`;
}

export function TablaPeriodos({ periodos, onEditar, onEliminar }: TablaPeriodosProps) {
  const [busqueda, setBusqueda] = useState("");
  const [filtroEstado, setFiltroEstado] = useState<FiltroEstado>("todos");

  const periodosOrdenadosYFiltrados = periodos
    .filter((p) => {
      const coincideNombre = p.nombre.toLowerCase().includes(busqueda.toLowerCase());
      const coincideEstado = filtroEstado === "todos" || p.estado === filtroEstado;
      return coincideNombre && coincideEstado;
    })
    .sort((a, b) => {
      if (b.anio !== a.anio) return b.anio - a.anio;
      return (ORDEN_CUATRIMESTRE[b.cuatrimestre] ?? 0) - (ORDEN_CUATRIMESTRE[a.cuatrimestre] ?? 0);
    });

  return (
    <Table>
      <Table.Toolbar
        left={
          <Input
            placeholder="Buscar por nombre…"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            style={{ width: 260 }}
          />
        }
        right={
          <Select
            value={filtroEstado}
            onChange={(e) => setFiltroEstado(e.target.value as FiltroEstado)}
            style={{ width: 180 }}
          >
            <option value="todos">Todos los estados</option>
            <option value="abierto">Abierto</option>
            <option value="proximo">Próximo</option>
            <option value="cerrado">Cerrado</option>
          </Select>
        }
      />
      <Table.Root>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Nombre</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 100 }}>Cuatr.</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 70 }}>Año</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 110 }}>Apertura</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 110 }}>Cierre</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 120 }}>Estado</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 90 }}>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {periodosOrdenadosYFiltrados.length === 0 ? (
            <Table.Row>
              <Table.Cell
                colSpan={7}
                style={{ textAlign: "center", color: "var(--color-text-tertiary)" }}
              >
                Sin resultados para los filtros aplicados.
              </Table.Cell>
            </Table.Row>
          ) : (
            periodosOrdenadosYFiltrados.map((periodo) => (
              <Table.Row key={periodo.id}>
                <Table.Cell>{periodo.nombre}</Table.Cell>
                <Table.Cell>{periodo.cuatrimestre}</Table.Cell>
                <Table.Cell numeric>{periodo.anio}</Table.Cell>
                <Table.Cell>{formatearFecha(periodo.fechaApertura)}</Table.Cell>
                <Table.Cell>{formatearFecha(periodo.fechaCierre)}</Table.Cell>
                <Table.Cell>
                  <EstadoPeriodoPill estado={periodo.estado} />
                </Table.Cell>
                <Table.Cell>
                  <MenuAccionesPeriodo
                    periodo={periodo}
                    onEditar={onEditar}
                    onEliminar={onEliminar}
                  />
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table.Root>
    </Table>
  );
}
