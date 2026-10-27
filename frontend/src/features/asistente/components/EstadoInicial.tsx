import { arrowRightIcon, sparkIcon } from "../../../app/shell/icons";
import type { CapacidadesDelAsistente } from "../types";

interface EstadoInicialProps {
  capacidades: CapacidadesDelAsistente;
  onElegir: (pregunta: string) => void;
  deshabilitado: boolean;
}

/**
 * Cuántos ejemplos entran en la grilla de la bienvenida (design spec § «Rediseño v3», tabla
 * «Bienvenida»: 2 columnas).
 */
const CANTIDAD_DE_EJEMPLOS_EN_LA_GRILLA = 4;

/** La pantalla vacía, armada SÓLO con el catálogo de `GET /capacidades`. */
export function EstadoInicial({ capacidades, onElegir, deshabilitado }: EstadoInicialProps) {
  const ejemplos = capacidades.ejemplos.slice(0, CANTIDAD_DE_EJEMPLOS_EN_LA_GRILLA);

  return (
    <div className="adoc-asistente-inicio">
      <span className="adoc-asistente-inicio-destello" aria-hidden="true">
        {sparkIcon}
      </span>

      <h2 className="adoc-asistente-inicio-titulo">¿Qué querés saber del sistema?</h2>

      {ejemplos.length > 0 && (
        <ul className="adoc-asistente-inicio-ejemplos" aria-label="Preguntas de ejemplo">
          {ejemplos.map((ejemplo) => (
            <li key={ejemplo}>
              <button
                type="button"
                className="adoc-asistente-inicio-ejemplo"
                disabled={deshabilitado}
                onClick={() => onElegir(ejemplo)}
              >
                <span>{ejemplo}</span>
                <span className="ico" aria-hidden="true">
                  {arrowRightIcon}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
