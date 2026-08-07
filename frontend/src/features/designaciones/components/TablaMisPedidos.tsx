import { Button } from "@ars-docendi/ui";
import type { ActorContexto, PedidoDesignacion } from "../types";
import { puedeEditarPedido, puedeEliminarPedido } from "../api/maquinaEstados";
import { EstadoPedidoPill } from "./EstadoPedidoPill";
import { IconoX } from "./lucide";
import { resumenMaterias } from "./detalleAdapters";

interface TablaMisPedidosProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  onVerDetalle: (pedido: PedidoDesignacion) => void;
  onEditar: (pedido: PedidoDesignacion) => void;
  onEliminar: (pedido: PedidoDesignacion) => void;
}

/** Etiqueta corta de la novedad (distingue cargo vs dedicación, como el diseño). */
function etiquetaNovedadCorta(p: PedidoDesignacion): string {
  if (p.novedad !== "Cambio de cargo o dedicación") return p.novedad;
  if (p.cargoSolicitado && p.cargoSolicitado !== p.cargoActual) return "Cambio de cargo";
  if (p.dedicacionSolicitada && p.dedicacionSolicitada !== p.dedicacionActual) {
    return "Cambio de dedicación";
  }
  return "Cambio de cargo o dedicación";
}

/** Fecha de envío (o de creación si sigue en borrador), formato dd/mm/aaaa. */
function fechaEnviado(p: PedidoDesignacion): string {
  const evento =
    p.historial.find((h) => h.accion === "enviar") ??
    p.historial.find((h) => h.accion === "crear") ??
    p.historial.at(0);
  if (!evento) return "—";
  const fecha = new Date(evento.fecha);
  const dia = String(fecha.getUTCDate()).padStart(2, "0");
  const mes = String(fecha.getUTCMonth() + 1).padStart(2, "0");
  return `${dia}/${mes}/${fecha.getUTCFullYear()}`;
}

/**
 * Tabla de "Mis pedidos": Nº · Docente · Legajo · Cátedra · Tipo · Enviado · Estado.
 * Cada fila navega al detalle al hacer click; "Ver"/"Editar" (mismo formato que en Usuarios) y la
 * X roja de eliminar (solo en borrador) no disparan esa navegación.
 */
export function TablaMisPedidos({
  pedidos,
  actor,
  onVerDetalle,
  onEditar,
  onEliminar,
}: TablaMisPedidosProps) {
  return (
    <div className="adoc-mp-table" role="table" aria-label="Mis pedidos de designación">
      <div className="adoc-mp-head" role="row">
        <span role="columnheader">N°</span>
        <span role="columnheader">DOCENTE</span>
        <span role="columnheader">LEGAJO</span>
        <span role="columnheader">CÁTEDRA</span>
        <span role="columnheader">TIPO</span>
        <span role="columnheader">ENVIADO</span>
        <span role="columnheader">ESTADO</span>
        <span role="columnheader" aria-label="Acciones" />
      </div>
      {pedidos.map((pedido) => (
        <div
          className="adoc-mp-row adoc-mp-row--clickeable"
          role="row"
          key={pedido.id}
          tabIndex={0}
          aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
          onClick={() => onVerDetalle(pedido)}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              onVerDetalle(pedido);
            }
          }}
        >
          <span className="adoc-mp-num" role="cell">
            {pedido.numero ?? "—"}
          </span>
          <span className="adoc-mp-doc" role="cell">
            {pedido.docente.nombre}
          </span>
          <span className="adoc-mp-leg" role="cell">
            {pedido.docente.legajo ?? "—"}
          </span>
          <span className="adoc-mp-cat" role="cell">
            {resumenMaterias(pedido.asignaciones)}
          </span>
          <span className="adoc-mp-nov" role="cell">
            {etiquetaNovedadCorta(pedido)}
          </span>
          <span className="adoc-mp-env" role="cell">
            {fechaEnviado(pedido)}
          </span>
          <span className="adoc-mp-est" role="cell">
            <EstadoPedidoPill estado={pedido.estado} />
          </span>
          <span className="adoc-mp-acc" role="cell">
            <Button
              variant="ghost"
              size="sm"
              onClick={(e) => {
                e.stopPropagation();
                onVerDetalle(pedido);
              }}
            >
              Ver
            </Button>
            {puedeEditarPedido(pedido, actor) && (
              <Button
                variant="ghost"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  onEditar(pedido);
                }}
              >
                Editar
              </Button>
            )}
            {puedeEliminarPedido(pedido, actor) && (
              <button
                type="button"
                className="adoc-mp-eliminar"
                aria-label={`Eliminar pedido de ${pedido.docente.nombre}`}
                onClick={(e) => {
                  e.stopPropagation();
                  onEliminar(pedido);
                }}
              >
                <IconoX />
              </button>
            )}
          </span>
        </div>
      ))}
    </div>
  );
}
