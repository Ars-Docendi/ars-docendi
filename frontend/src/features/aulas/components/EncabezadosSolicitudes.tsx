import { Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";
import type { EstadoSolicitudAula } from "../types";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import {
  etiquetaEstado,
  type ColumnaOrdenSolicitudes,
  type OrdenSolicitudes,
} from "./filtrosSolicitudes";

/** Encabezado ordenable con filtro, compartido por las dos tablas de solicitudes. */
export function Encabezado({
  etiqueta,
  columna,
  orden,
  onOrden,
  filtro,
  activo,
  onLimpiar,
}: {
  etiqueta: string;
  columna: ColumnaOrdenSolicitudes;
  orden: OrdenSolicitudes | null;
  onOrden: (columna: ColumnaOrdenSolicitudes) => void;
  filtro: ReactNode;
  activo: boolean;
  onLimpiar: () => void;
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
          {filtro}
        </FiltroEncabezado>
      </span>
    </Table.HeaderCell>
  );
}

/** Opciones de estado (selección múltiple) del filtro de la columna Estado. */
export function Opciones({
  opciones,
  valores,
  onToggle,
}: {
  opciones: EstadoSolicitudAula[];
  valores: EstadoSolicitudAula[];
  onToggle: (valor: EstadoSolicitudAula) => void;
}) {
  return (
    <div className="adoc-filtro-encabezado-opciones">
      {opciones.map((opcion) => (
        <label className="adoc-filtro-encabezado-opcion" key={opcion}>
          <input
            type="checkbox"
            checked={valores.includes(opcion)}
            onChange={() => onToggle(opcion)}
          />
          {etiquetaEstado(opcion)}
        </label>
      ))}
    </div>
  );
}
