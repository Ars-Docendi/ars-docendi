import { Input, Table } from "@ars-docendi/ui";
import { useState } from "react";
import type { ReactNode } from "react";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import type { PeriodoDesignacion } from "../types";
import {
  aplicarFiltrosYOrdenPeriodos,
  formatearFecha,
  formatearMesAnio,
  FILTROS_PERIODOS_INICIALES,
  siguienteOrdenPeriodos,
  type ColumnaOrdenPeriodos,
  type FiltroEstadoPeriodo,
  type FiltrosPeriodos,
  type OrdenPeriodos,
} from "./filtrosPeriodos";
import { MenuAccionesPeriodo } from "./MenuAccionesPeriodo";

interface TablaPeriodosProps {
  periodos: PeriodoDesignacion[];
  onEditar: (periodo: PeriodoDesignacion) => void;
  onEliminar: (periodo: PeriodoDesignacion) => void;
}

/** Tabla de períodos; los filtros y el orden son locales porque no hay paginación. */
export function TablaPeriodos({ periodos, onEditar, onEliminar }: TablaPeriodosProps) {
  const [filtros, setFiltros] = useState<FiltrosPeriodos>(FILTROS_PERIODOS_INICIALES);
  const [orden, setOrden] = useState<OrdenPeriodos | null>(null);
  const visibles = aplicarFiltrosYOrdenPeriodos(periodos, filtros, orden);

  function cambiarFiltro<K extends keyof FiltrosPeriodos>(campo: K, valor: FiltrosPeriodos[K]) {
    setFiltros((actual) => ({ ...actual, [campo]: valor }));
  }

  function alternarActivo(valor: FiltroEstadoPeriodo) {
    const valores = filtros.activo;
    cambiarFiltro(
      "activo",
      valores.includes(valor) ? valores.filter((actual) => actual !== valor) : [...valores, valor],
    );
  }

  function limpiar(campo: keyof FiltrosPeriodos) {
    cambiarFiltro(campo, (Array.isArray(filtros[campo]) ? [] : "") as never);
  }

  return (
    <Table className="adoc-periodos-table">
      <Table.Root>
        <Table.Head>
          <Table.Row>
            <Encabezado
              etiqueta="Nombre"
              columna="nombre"
              orden={orden}
              onOrden={(columna) => setOrden(siguienteOrdenPeriodos(orden, columna))}
              activo={Boolean(filtros.nombre.trim())}
              onLimpiar={() => limpiar("nombre")}
            >
              <Input
                className="adoc-filtro-encabezado-campo"
                placeholder="Buscar nombre…"
                aria-label="Buscar Nombre"
                value={filtros.nombre}
                onChange={(evento) => cambiarFiltro("nombre", evento.target.value)}
              />
            </Encabezado>
            <Encabezado
              etiqueta="Carga desde"
              columna="cargaDesde"
              orden={orden}
              onOrden={(columna) => setOrden(siguienteOrdenPeriodos(orden, columna))}
              activo={Boolean(filtros.cargaDesde.trim())}
              onLimpiar={() => limpiar("cargaDesde")}
            >
              <Input
                className="adoc-filtro-encabezado-campo"
                placeholder="Buscar fecha…"
                aria-label="Buscar Carga desde"
                value={filtros.cargaDesde}
                onChange={(evento) => cambiarFiltro("cargaDesde", evento.target.value)}
              />
            </Encabezado>
            <Encabezado
              etiqueta="Carga hasta"
              columna="cargaHasta"
              orden={orden}
              onOrden={(columna) => setOrden(siguienteOrdenPeriodos(orden, columna))}
              activo={Boolean(filtros.cargaHasta.trim())}
              onLimpiar={() => limpiar("cargaHasta")}
            >
              <Input
                className="adoc-filtro-encabezado-campo"
                placeholder="Buscar fecha…"
                aria-label="Buscar Carga hasta"
                value={filtros.cargaHasta}
                onChange={(evento) => cambiarFiltro("cargaHasta", evento.target.value)}
              />
            </Encabezado>
            <Encabezado
              etiqueta="Impacto desde"
              columna="impactoDesde"
              orden={orden}
              onOrden={(columna) => setOrden(siguienteOrdenPeriodos(orden, columna))}
              activo={Boolean(filtros.impactoDesde.trim())}
              onLimpiar={() => limpiar("impactoDesde")}
            >
              <Input
                className="adoc-filtro-encabezado-campo"
                placeholder="Buscar mes o fecha…"
                aria-label="Buscar Impacto desde"
                value={filtros.impactoDesde}
                onChange={(evento) => cambiarFiltro("impactoDesde", evento.target.value)}
              />
            </Encabezado>
            <Encabezado
              etiqueta="Impacto hasta"
              columna="impactoHasta"
              orden={orden}
              onOrden={(columna) => setOrden(siguienteOrdenPeriodos(orden, columna))}
              activo={Boolean(filtros.impactoHasta.trim())}
              onLimpiar={() => limpiar("impactoHasta")}
            >
              <Input
                className="adoc-filtro-encabezado-campo"
                placeholder="Buscar mes o fecha…"
                aria-label="Buscar Impacto hasta"
                value={filtros.impactoHasta}
                onChange={(evento) => cambiarFiltro("impactoHasta", evento.target.value)}
              />
            </Encabezado>
            <Encabezado
              etiqueta="Activo"
              columna="activo"
              orden={orden}
              onOrden={(columna) => setOrden(siguienteOrdenPeriodos(orden, columna))}
              activo={filtros.activo.length > 0}
              onLimpiar={() => limpiar("activo")}
            >
              <Opciones valores={filtros.activo} onToggle={alternarActivo} />
            </Encabezado>
            <Table.HeaderCell>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {visibles.length === 0 ? (
            <Table.Row>
              <Table.Cell
                colSpan={7}
                style={{ textAlign: "center", color: "var(--color-text-tertiary)" }}
              >
                Sin resultados para los filtros aplicados.
              </Table.Cell>
            </Table.Row>
          ) : (
            visibles.map((periodo) => (
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

function Encabezado({
  etiqueta,
  columna,
  orden,
  onOrden,
  activo,
  onLimpiar,
  children,
}: {
  etiqueta: string;
  columna: ColumnaOrdenPeriodos;
  orden: OrdenPeriodos | null;
  onOrden: (columna: ColumnaOrdenPeriodos) => void;
  activo: boolean;
  onLimpiar: () => void;
  children: ReactNode;
}) {
  return (
    <Table.HeaderCell
      aria-label={etiqueta}
      sort={orden?.columna === columna ? orden.direccion : null}
      onSortChange={() => onOrden(columna)}
    >
      <span>
        {etiqueta}
        <FiltroEncabezado etiqueta={etiqueta} activo={activo} onLimpiar={onLimpiar}>
          {children}
        </FiltroEncabezado>
      </span>
    </Table.HeaderCell>
  );
}

function Opciones({
  valores,
  onToggle,
}: {
  valores: FiltroEstadoPeriodo[];
  onToggle: (valor: FiltroEstadoPeriodo) => void;
}) {
  return (
    <div className="adoc-filtro-encabezado-opciones">
      {(["activo", "inactivo"] as const).map((opcion) => (
        <label className="adoc-filtro-encabezado-opcion" key={opcion}>
          <input
            type="checkbox"
            checked={valores.includes(opcion)}
            onChange={() => onToggle(opcion)}
          />
          {opcion === "activo" ? "Activo" : "Inactivo"}
        </label>
      ))}
    </div>
  );
}
