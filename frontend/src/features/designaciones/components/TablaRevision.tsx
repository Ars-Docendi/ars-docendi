import { useState } from "react";
import { Button, Table, Tabs } from "@ars-docendi/ui";
import type { ActorContexto, PedidoDesignacion } from "../types";
import {
  PESTANIAS,
  areaActual,
  etiquetaEstado,
  inicialesDocente,
  inicioEnCircuito,
  ordenarPedidos,
  pedidosDePestania,
  pestaniaInicial,
  siguienteOrden,
  ultimaActualizacion,
  type ColumnaOrdenable,
  type IdPestania,
  type OrdenTabla,
} from "./tableroRevisionModelo";
import { aplicarFiltros, type FiltrosTablero } from "./filtrosTablero";
import { NovedadChip } from "./NovedadChip";
import { EstadoPedidoBadge } from "./EstadoPedidoBadge";
import "./revision.css";

interface TablaRevisionProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  filtros: FiltrosTablero;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/** Columnas ordenables y su rótulo. "Área" y "Acciones" quedan fuera a propósito. */
const COLUMNAS: { id: ColumnaOrdenable; etiqueta: string }[] = [
  { id: "docente", etiqueta: "Docente" },
  { id: "legajo", etiqueta: "Legajo" },
  { id: "tipo", etiqueta: "Tipo" },
  { id: "inicio", etiqueta: "Inicio" },
  { id: "ultima", etiqueta: "Últ. actualización" },
  { id: "estado", etiqueta: "Estado" },
];

/**
 * Tabla de revisión: una sola tabla (`Table` del design system, igual que
 * Usuarios/Docentes/Períodos) con pestañas por área del circuito arriba y los
 * filtros en su `Table.Toolbar`.
 *
 * Los filtros NO viven acá sino arriba de las pestañas (los renderiza
 * `TableroRevisionPage`): se aplican al ámbito entero —los contadores de las
 * pestañas ya salen filtrados— así que meterlos dentro de la tabla los hacía
 * leer como si fueran de la pestaña abierta.
 */
export function TablaRevision({ pedidos, actor, filtros, onSeleccionar }: TablaRevisionProps) {
  const [pestania, setPestania] = useState<IdPestania>(() => pestaniaInicial(actor));
  const [orden, setOrden] = useState<OrdenTabla | null>(null);

  const filtrados = aplicarFiltros(pedidos, filtros);
  const items = PESTANIAS.map(({ id, etiqueta }) => ({
    id,
    label: etiqueta,
    count: pedidosDePestania(filtrados, id).length,
  }));
  const visibles = ordenarPedidos(pedidosDePestania(filtrados, pestania), orden);
  // El área solo aporta en "Todos": en una pestaña de área es constante en todas las
  // filas y ya la dice la pestaña. En Finalizados no hay área que mostrar.
  const mostrarArea = pestania === "todos";

  return (
    <div className="adoc-revision">
      <Tabs
        items={items}
        value={pestania}
        onChange={(id) => setPestania(id as IdPestania)}
        aria-label="Área del circuito"
      />

      <div className="adoc-tabla-scroll">
        <Table>
          {visibles.length === 0 ? (
            <p className="adoc-revision-vacio">Sin pedidos que cumplan los filtros.</p>
          ) : (
            <Table.Root>
              <Table.Head>
                <Table.Row>
                  {COLUMNAS.map(({ id, etiqueta }) => (
                    <Table.HeaderCell
                      key={id}
                      sort={orden?.columna === id ? orden.direccion : null}
                      onSortChange={() => setOrden((previo) => siguienteOrden(previo, id))}
                    >
                      {etiqueta}
                    </Table.HeaderCell>
                  ))}
                  {mostrarArea && <Table.HeaderCell>Área</Table.HeaderCell>}
                  <Table.HeaderCell>Acciones</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {visibles.map((pedido) => (
                  <FilaPedido
                    key={pedido.id}
                    pedido={pedido}
                    mostrarArea={mostrarArea}
                    onVer={onSeleccionar}
                  />
                ))}
              </Table.Body>
            </Table.Root>
          )}
        </Table>
      </div>
    </div>
  );
}

function FilaPedido({
  pedido,
  mostrarArea,
  onVer,
}: {
  pedido: PedidoDesignacion;
  mostrarArea: boolean;
  onVer: (pedido: PedidoDesignacion) => void;
}) {
  return (
    <Table.Row>
      <Table.Cell>
        <span className="adoc-tabla-docente">
          <span className="adoc-pedido-avatar" aria-hidden="true">
            {inicialesDocente(pedido.docente.nombre)}
          </span>
          <span className="adoc-tabla-nombre">{pedido.docente.nombre}</span>
        </span>
      </Table.Cell>
      <Table.Cell className="adoc-mono">{pedido.docente.legajo ?? "—"}</Table.Cell>
      <Table.Cell>
        <NovedadChip novedad={pedido.novedad} />
      </Table.Cell>
      <Table.Cell>{inicioEnCircuito(pedido) ?? "—"}</Table.Cell>
      <Table.Cell>{ultimaActualizacion(pedido) ?? "—"}</Table.Cell>
      <Table.Cell>
        <EstadoPedidoBadge
          estado={pedido.estado}
          prioritario={pedido.prioritario}
          etiqueta={etiquetaEstado(pedido)}
        />
      </Table.Cell>
      {mostrarArea && <Table.Cell>{areaActual(pedido) ?? "—"}</Table.Cell>}
      <Table.Cell className="adoc-table-actions">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onVer(pedido)}
          aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
        >
          Ver
        </Button>
      </Table.Cell>
    </Table.Row>
  );
}
