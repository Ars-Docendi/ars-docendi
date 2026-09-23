import { Button, Input, Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";
import type { SolicitudReservaAula } from "../types";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import {
  etiquetaEstado,
  fechaSolicitud,
  horarioSolicitud,
  opcionesEstados,
  siguienteOrdenSolicitudes,
  type ColumnaOrdenSolicitudes,
  type FiltrosSolicitudesState,
  type OrdenSolicitudes,
} from "./filtrosSolicitudes";
import { EstadoSolicitudPill } from "./EstadoSolicitudPill";

interface TablaMisSolicitudesProps {
  solicitudes: SolicitudReservaAula[];
  solicitudesParaOpciones?: SolicitudReservaAula[];
  filtros?: FiltrosSolicitudesState;
  orden?: OrdenSolicitudes | null;
  onFiltrosChange?: (filtros: FiltrosSolicitudesState) => void;
  onOrdenChange?: (orden: OrdenSolicitudes | null) => void;
  onCancelar: (solicitud: SolicitudReservaAula) => void;
  /** Doble click sobre una fila Rechazada: abre el popup de detalle con el motivo. */
  onVerDetalle: (solicitud: SolicitudReservaAula) => void;
}

const SIN_CAMBIOS = () => {};

/** Tabla "Mis solicitudes" del Docente, con filtros y orden por encabezado. */
export function TablaMisSolicitudes({
  solicitudes,
  solicitudesParaOpciones = solicitudes,
  filtros = { dia: "", materia: "", comision: "", docente: "", estado: [] },
  orden = null,
  onFiltrosChange = SIN_CAMBIOS,
  onOrdenChange = SIN_CAMBIOS,
  onCancelar,
  onVerDetalle,
}: TablaMisSolicitudesProps) {
  const estados = opcionesEstados(solicitudesParaOpciones);

  function cambiarFiltro<K extends keyof FiltrosSolicitudesState>(
    campo: K,
    valor: FiltrosSolicitudesState[K],
  ) {
    onFiltrosChange({ ...filtros, [campo]: valor });
  }

  function cambiarOrden(columna: ColumnaOrdenSolicitudes) {
    onOrdenChange(siguienteOrdenSolicitudes(orden, columna));
  }

  function alternarEstado(valor: string) {
    const valores = filtros.estado;
    cambiarFiltro(
      "estado",
      (valores.includes(valor as never)
        ? valores.filter((actual) => actual !== valor)
        : [...valores, valor]) as FiltrosSolicitudesState["estado"],
    );
  }

  return (
    <Table className="adoc-aulas-table">
      <Table.Root aria-label="Mis solicitudes de reserva de aula">
        <Table.Head>
          <Table.Row>
            <Encabezado
              etiqueta="Día"
              columna="dia"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar día…"
                  aria-label="Buscar Día"
                  value={filtros.dia}
                  onChange={(e) => cambiarFiltro("dia", e.target.value)}
                />
              }
              activo={Boolean(filtros.dia.trim())}
              onLimpiar={() => cambiarFiltro("dia", "")}
            />
            <Table.HeaderCell
              sort={orden?.columna === "horario" ? orden.direccion : null}
              onSortChange={() => cambiarOrden("horario")}
            >
              Horario
            </Table.HeaderCell>
            <Table.HeaderCell
              sort={orden?.columna === "alumnos" ? orden.direccion : null}
              onSortChange={() => cambiarOrden("alumnos")}
            >
              Capacidad
            </Table.HeaderCell>
            <Table.HeaderCell>Cód. Materia</Table.HeaderCell>
            <Encabezado
              etiqueta="Materia"
              columna="materia"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar materia…"
                  aria-label="Buscar Materia"
                  value={filtros.materia}
                  onChange={(e) => cambiarFiltro("materia", e.target.value)}
                />
              }
              activo={Boolean(filtros.materia.trim())}
              onLimpiar={() => cambiarFiltro("materia", "")}
            />
            <Encabezado
              etiqueta="Comisión"
              columna="comision"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar comisión…"
                  aria-label="Buscar Comisión"
                  value={filtros.comision}
                  onChange={(e) => cambiarFiltro("comision", e.target.value)}
                />
              }
              activo={Boolean(filtros.comision.trim())}
              onLimpiar={() => cambiarFiltro("comision", "")}
            />
            <Encabezado
              etiqueta="Estado"
              columna="estado"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Opciones opciones={estados} valores={filtros.estado} onToggle={alternarEstado} />
              }
              activo={filtros.estado.length > 0}
              onLimpiar={() => cambiarFiltro("estado", [])}
            />
            <Table.HeaderCell>Aula</Table.HeaderCell>
            <Table.HeaderCell>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {solicitudes.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={9} className="empty">
                Sin resultados para los filtros aplicados.
              </Table.Cell>
            </Table.Row>
          ) : (
            solicitudes.map((solicitud) => (
              <Table.Row
                key={solicitud.id}
                className={
                  solicitud.estado === "rechazada" ? "adoc-aulas-row--accionable" : undefined
                }
                title={
                  solicitud.estado === "rechazada" ? "Doble click para ver el motivo" : undefined
                }
                onDoubleClick={() => {
                  if (solicitud.estado === "rechazada") onVerDetalle(solicitud);
                }}
              >
                <Table.Cell>{fechaSolicitud(solicitud)}</Table.Cell>
                <Table.Cell>{horarioSolicitud(solicitud)}</Table.Cell>
                <Table.Cell>{solicitud.cantidadAlumnosAprox}</Table.Cell>
                <Table.Cell>{solicitud.materia.codigo}</Table.Cell>
                <Table.Cell>{solicitud.materia.nombre}</Table.Cell>
                <Table.Cell>{solicitud.comision}</Table.Cell>
                <Table.Cell>
                  <EstadoSolicitudPill estado={solicitud.estado} />
                </Table.Cell>
                <Table.Cell>{solicitud.aulaAsignada ?? "—"}</Table.Cell>
                <Table.Cell>
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={solicitud.estado !== "pendiente"}
                    onClick={() => onCancelar(solicitud)}
                  >
                    Cancelar
                  </Button>
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

function Opciones({
  opciones,
  valores,
  onToggle,
}: {
  opciones: string[];
  valores: string[];
  onToggle: (valor: string) => void;
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
          {etiquetaEstado(opcion as never)}
        </label>
      ))}
    </div>
  );
}
