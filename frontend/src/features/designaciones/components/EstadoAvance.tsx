import type { PedidoDesignacion } from "../types";
import { avancePedido, TOTAL_PASOS } from "./tableroRevisionModelo";

/**
 * Celda Estado de la vista Tabla: combina estado + avance del circuito en una
 * sola pieza, en una línea.
 * - En revisión (pasos 1..3): mini-stepper accent parcial + "En {etapa} · x/4".
 * - Aceptado (`en_lote`): stepper completo verde + "Aceptado".
 * - Devuelto / Rechazado: dot de color + etiqueta del estado.
 *
 * Reusa las clases del stepper de la card (`adoc-pedido-stepper*`) para no
 * driftear los tokens entre la Tabla y el Tablero.
 */
export function EstadoAvance({ pedido }: { pedido: PedidoDesignacion }) {
  const avance = avancePedido(pedido);

  if (avance && avance.paso < avance.total) {
    return (
      <span className="adoc-estado-avance acento">
        <Stepper paso={avance.paso} total={avance.total} />
        <span className="adoc-estado-avance-txt">
          {avance.etiqueta} · {avance.paso}/{avance.total}
        </span>
      </span>
    );
  }

  if (pedido.estado === "en_lote") {
    return (
      <span className="adoc-estado-avance exito">
        <Stepper paso={TOTAL_PASOS} total={TOTAL_PASOS} variante="exito" />
        <span className="adoc-estado-avance-txt">Aceptado</span>
      </span>
    );
  }

  const tono = pedido.estado === "devuelto" ? "alerta" : "peligro";
  const etiqueta = pedido.estado === "devuelto" ? "Devuelto" : "Rechazado";
  return (
    <span className={`adoc-estado-avance ${tono}`}>
      <span className="adoc-estado-dot" aria-hidden="true" />
      <span className="adoc-estado-avance-txt">{etiqueta}</span>
    </span>
  );
}

function Stepper({ paso, total, variante }: { paso: number; total: number; variante?: "exito" }) {
  return (
    <span className={`adoc-pedido-stepper${variante ? ` ${variante}` : ""}`} aria-hidden="true">
      {Array.from({ length: total }, (_, indice) => (
        <span key={indice} className={`adoc-pedido-stepper-bar${indice < paso ? " lleno" : ""}`} />
      ))}
    </span>
  );
}
