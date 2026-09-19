import { Button } from "@ars-docendi/ui";

import { IndicadorDeProceso, UMBRAL_DE_APARICION_MS } from "./IndicadorDeProceso";
import { LineaDeMetricas } from "./LineaDeMetricas";
import { stopIcon } from "../../../app/shell/icons";
import { useVisibleTrasUmbral } from "../hooks/useVisibleTrasUmbral";
import type { TurnoDeLaConversacion } from "../types";

interface FranjaDeEstadoProps {
  enVuelo: boolean;
  turnos: TurnoDeLaConversacion[];
  /** Dejar de esperar el turno en vuelo. */
  onDetener: () => void;
  /** Para el test del umbral, que no puede esperar el tiempo real. */
  umbralMs?: number;
}

/**
 * La fila entre el hilo y el campo de entrada: a la izquierda el estado del turno
 * y, mientras se espera, «Dejar de esperar»; a la derecha lo que costó el último.
 *
 * Casi nunca se ven a la vez —el indicador mientras se espera, las métricas cuando
 * ya llegó—, así que en dos filas separadas una estaba vacía la mayor parte del
 * tiempo y el campo de entrada saltaba cada vez que la otra aparecía.
 *
 * Los dos siguen FUERA de la región viva y con el contrato de siempre: el
 * indicador es un `role="status"` con umbral y un solo texto; las métricas van
 * ocultas al lector. Cambia dónde están, no lo que anuncian.
 */
export function FranjaDeEstado({
  enVuelo,
  turnos,
  onDetener,
  umbralMs = UMBRAL_DE_APARICION_MS,
}: FranjaDeEstadoProps) {
  // Aparece con el indicador y no antes: un botón que se ve un instante en cada
  // respuesta determinista es el mismo parpadeo que el umbral le evita al texto.
  const mostrarDetener = useVisibleTrasUmbral(enVuelo, umbralMs);

  return (
    <div className="adoc-asistente-franja">
      <IndicadorDeProceso activo={enVuelo} umbralMs={umbralMs} />

      {mostrarDetener && (
        // «Dejar de esperar» y no «Detener» ni «Cancelar»: suelta el request de
        // este lado y libera el campo, y eso es todo lo que hace. El backend sigue
        // el turno hasta el final y lo cobra; ni el nombre ni ningún tooltip
        // insinúan otra cosa.
        <Button variant="ghost" size="sm" leadingIcon={stopIcon} onClick={onDetener}>
          Dejar de esperar
        </Button>
      )}

      <LineaDeMetricas turnos={turnos} />
    </div>
  );
}
