import type { PedidoDesignacion } from "../types";
import { EstadoPedidoPill } from "./EstadoPedidoPill";
import { MenuAccionesPedido } from "./MenuAccionesPedido";
import { resumenMaterias } from "./detalleAdapters";

interface TablaMisPedidosProps {
  pedidos: PedidoDesignacion[];
  onVerDetalle: (pedido: PedidoDesignacion) => void;
  onEditar: (pedido: PedidoDesignacion) => void;
  onEnviar: (pedido: PedidoDesignacion) => void;
  onCancelar: (pedido: PedidoDesignacion) => void;
  onReenviar: (pedido: PedidoDesignacion) => void;
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

/** Tabla de "Mis pedidos" (1:1 con screens.pen): Nº · Docente · Cátedra · Novedad · Enviado · Estado · ⋮. */
export function TablaMisPedidos({
  pedidos,
  onVerDetalle,
  onEditar,
  onEnviar,
  onCancelar,
  onReenviar,
}: TablaMisPedidosProps) {
  return (
    <div className="adoc-mp-table" role="table" aria-label="Mis pedidos de designación">
      <div className="adoc-mp-head" role="row">
        <span role="columnheader">N°</span>
        <span role="columnheader">DOCENTE</span>
        <span role="columnheader">CÁTEDRA</span>
        <span role="columnheader">NOVEDAD</span>
        <span role="columnheader">ENVIADO</span>
        <span role="columnheader">ESTADO</span>
        <span role="columnheader" aria-label="Acciones" />
      </div>
      {pedidos.map((pedido) => (
        <div className="adoc-mp-row" role="row" key={pedido.id}>
          <span className="adoc-mp-num" role="cell">
            {pedido.numero ?? "—"}
          </span>
          <span className="adoc-mp-doc" role="cell">
            Prof. {pedido.docente.nombre}
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
            <MenuAccionesPedido
              pedido={pedido}
              onVerDetalle={onVerDetalle}
              onEditar={onEditar}
              onEnviar={onEnviar}
              onCancelar={onCancelar}
              onReenviar={onReenviar}
            />
          </span>
        </div>
      ))}
    </div>
  );
}
