import type { PedidoDesignacion } from "../types";
import {
  avanceEtapaRetorno,
  avancePedido,
  quienDevolvio,
  rolDeQuienDevolvio,
} from "./tableroRevisionModelo";

/**
 * Celda Estado de la vista Tabla: combina estado + avance del circuito en una
 * sola pieza, en una línea.
 * - En revisión (pasos 1..3): mini-stepper accent parcial + "En {etapa} · x/4".
 * - Devuelto: el MISMO formato stepper + "En {etapa} · x/4" que un estado en
 *   revisión — pero calculado sobre `etapaRetorno` (la etapa a la que
 *   vuelve), no `estado` — con "Devuelto por {revisor} ({rol})" agregado al
 *   costado, no en su lugar: sin la etapa, la fila perdía la referencia al
 *   estado real del pedido (lo que un revisor necesita para "Mis
 *   pendientes").
 * - Aceptado (`en_lote`): dot de color + "Aceptado" — es un estado terminal,
 *   no hay avance que mostrar, así que no lleva stepper.
 * - Rechazado: dot de color + "Rechazado" — ídem, terminal sin avance.
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

  if (pedido.estado === "devuelto") {
    const avanceRetorno = avanceEtapaRetorno(pedido);
    const revisor = quienDevolvio(pedido) ?? "—";
    const rol = rolDeQuienDevolvio(pedido);
    const detalle = rol ? `Devuelto por ${revisor} (${rol})` : `Devuelto por ${revisor}`;
    return (
      <span className="adoc-estado-avance alerta">
        {avanceRetorno ? (
          <Stepper paso={avanceRetorno.paso} total={avanceRetorno.total} />
        ) : (
          <span className="adoc-estado-dot" aria-hidden="true" />
        )}
        <span className="adoc-estado-avance-txt">
          {avanceRetorno ? (
            <>
              {avanceRetorno.etiqueta} · {avanceRetorno.paso}/{avanceRetorno.total} · {detalle}
            </>
          ) : (
            detalle
          )}
        </span>
      </span>
    );
  }

  const tono = pedido.estado === "en_lote" ? "exito" : "peligro";
  const etiqueta = pedido.estado === "en_lote" ? "Aceptado" : "Rechazado";
  return (
    <span className={`adoc-estado-avance ${tono}`}>
      <span className="adoc-estado-dot" aria-hidden="true" />
      <span className="adoc-estado-avance-txt">{etiqueta}</span>
    </span>
  );
}

function Stepper({ paso, total }: { paso: number; total: number }) {
  return (
    <span className="adoc-pedido-stepper" aria-hidden="true">
      {Array.from({ length: total }, (_, indice) => (
        <span key={indice} className={`adoc-pedido-stepper-bar${indice < paso ? " lleno" : ""}`} />
      ))}
    </span>
  );
}
