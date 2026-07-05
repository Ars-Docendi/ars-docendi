import { useState } from "react";
import { Table, Input, Select } from "@ars-docendi/ui";
import type { PeriodoDesignacion } from "../types";
import { MenuAccionesPeriodo } from "./MenuAccionesPeriodo";

interface TablaPeriodosProps {
  periodos: PeriodoDesignacion[];
  onEditar: (periodo: PeriodoDesignacion) => void;
  onEliminar: (periodo: PeriodoDesignacion) => void;
}

type FiltroActivo = "todos" | "activos" | "inactivos";

function formatearFecha(fechaIso: string): string {
  const [anio, mes, dia] = fechaIso.split("-");
  return `${dia}/${mes}/${anio}`;
}

const MESES = [
  "Enero",
  "Febrero",
  "Marzo",
  "Abril",
  "Mayo",
  "Junio",
  "Julio",
  "Agosto",
  "Septiembre",
  "Octubre",
  "Noviembre",
  "Diciembre",
];

function formatearMesAnio(fechaIso: string): string {
  const [anio, mes] = fechaIso.split("-");
  return `${MESES[Number(mes) - 1]} ${anio}`;
}

export function TablaPeriodos({ periodos, onEditar, onEliminar }: TablaPeriodosProps) {
  const [busqueda, setBusqueda] = useState("");
  const [filtroActivo, setFiltroActivo] = useState<FiltroActivo>("todos");

  const periodosOrdenadosYFiltrados = periodos
    .filter((p) => {
      const coincideNombre = p.nombre.toLowerCase().includes(busqueda.toLowerCase());
      const coincideActivo =
        filtroActivo === "todos" || (filtroActivo === "activos" ? p.activo : !p.activo);
      return coincideNombre && coincideActivo;
    })
    .sort((a, b) => (b.impactoDesde < a.impactoDesde ? -1 : 1));

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
            value={filtroActivo}
            onChange={(e) => setFiltroActivo(e.target.value as FiltroActivo)}
            style={{ width: 180 }}
          >
            <option value="todos">Todos</option>
            <option value="activos">Activos</option>
            <option value="inactivos">Inactivos</option>
          </Select>
        }
      />
      <Table.Root>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Nombre</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 110 }}>Carga desde</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 110 }}>Carga hasta</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 130 }}>Impacto desde</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 130 }}>Impacto hasta</Table.HeaderCell>
            <Table.HeaderCell style={{ width: 90 }}>Activo</Table.HeaderCell>
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
                <Table.Cell>{formatearFecha(periodo.cargaDesde)}</Table.Cell>
                <Table.Cell>{formatearFecha(periodo.cargaHasta)}</Table.Cell>
                <Table.Cell>{formatearMesAnio(periodo.impactoDesde)}</Table.Cell>
                <Table.Cell>{formatearMesAnio(periodo.impactoHasta)}</Table.Cell>
                <Table.Cell>{periodo.activo ? "Activo" : "Inactivo"}</Table.Cell>
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
