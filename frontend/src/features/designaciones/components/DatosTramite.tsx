import type { ReactNode } from "react";
import type { EstadoPedido, PedidoDesignacion } from "../types";
import { formatearFecha, posicionEtapa } from "./detalleAdapters";

/** Etiqueta corta de la etapa actual del trámite. */
const ETAPA_LEGIBLE: Record<EstadoPedido, string> = {
  borrador: "Borrador",
  en_revision_coordinador: "Coordinador",
  en_revision_secretaria: "Secretaría",
  en_revision_decanato: "Decanato",
  devuelto: "Devuelto",
  en_lote: "En lote",
  rechazado: "Rechazado",
  cancelado: "Cancelado",
};

const ICONO_BANDERA = (
  <svg viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
    <path d="M4 2a.85.85 0 0 0-.85.85V14h1.5V9.6h6.2l-1.1-2 1.1-2H4.65V3.5h7L9.9 5.3l1.75 1.8H4.65" />
    <rect x="3.15" y="2" width="1.5" height="12" />
  </svg>
);

/** Fila clave/valor del meta-panel. */
function MetaRow({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div className="adoc-meta-row">
      <span className="adoc-meta-k">{etiqueta}</span>
      <span className="adoc-meta-v">{children}</span>
    </div>
  );
}

/**
 * Meta-panel "Datos del trámite" (rail derecho): etapa, ámbito, prioridad y la
 * fecha de envío/creación. Sólo campos reales del `PedidoDesignacion` — los
 * campos de mockup del diseño sin fuente de datos (expediente, integridad) se
 * omiten en lugar de inventarse (invariante #7).
 */
export function DatosTramite({ pedido }: { pedido: PedidoDesignacion }) {
  const ultimoEnvio = [...pedido.historial]
    .reverse()
    .find((evento) => evento.accion === "reenviar" || evento.accion === "enviar");
  const creacion = pedido.historial.find((evento) => evento.accion === "crear");
  const posicion = posicionEtapa(pedido.estado);

  return (
    <section className="adoc-card adoc-meta">
      <header className="adoc-meta-head">
        <h2 className="adoc-meta-title">Datos del trámite</h2>
      </header>
      <div className="adoc-meta-body">
        <MetaRow etiqueta="Etapa">
          {ETAPA_LEGIBLE[pedido.estado]}
          {posicion && ` · ${posicion.n} de ${posicion.total}`}
        </MetaRow>
        <MetaRow etiqueta="Ámbito">{pedido.carrera}</MetaRow>
        <MetaRow etiqueta="Prioritario">
          {pedido.prioritario ? (
            <span className="adoc-meta-prio">
              {ICONO_BANDERA}
              Sí
            </span>
          ) : (
            "No"
          )}
        </MetaRow>
        {ultimoEnvio && (
          <MetaRow etiqueta={ultimoEnvio.accion === "reenviar" ? "Reenviado" : "Enviado"}>
            {formatearFecha(ultimoEnvio.fecha)}
          </MetaRow>
        )}
        {creacion && <MetaRow etiqueta="Creado">{formatearFecha(creacion.fecha)}</MetaRow>}
      </div>
    </section>
  );
}
