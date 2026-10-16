import { arrowRightIcon, sparkIcon } from "../../../app/shell/icons";
import type { CapacidadesDelAsistente } from "../types";

interface EstadoInicialProps {
  capacidades: CapacidadesDelAsistente;
  onElegir: (pregunta: string) => void;
  deshabilitado: boolean;
}

/**
 * Cuántos ejemplos entran en la grilla de la bienvenida (design spec § «Rediseño
 * v3», tabla «Bienvenida»: 2 columnas).
 *
 * El catálogo puede mandar hasta seis (`CatalogoDeCapacidades.MaximoDeEjemplos`);
 * acá se toman los primeros cuatro y se descarta el resto, no al revés — el
 * layout es fijo, y una grilla que creciera con el catálogo dejaría de ser 2×2.
 */
const CANTIDAD_DE_EJEMPLOS_EN_LA_GRILLA = 4;

/**
 * La pantalla vacía, armada SÓLO con el catálogo de `GET /capacidades`.
 *
 * Nada de acá se inventa en el cliente: el título es fijo (RF-15) y los ejemplos
 * los manda el backend, que es quien sabe qué puede responder para este usuario.
 * Son preguntas verificadas, así que una tarjeta es una pregunta que se sabe que
 * funciona, y se manda tal cual. Son, además, las ÚNICAS sugerencias clicables
 * que le quedan al asistente (ARS-140, ARS-149, design.md D12 de
 * asistente-rediseno-v3): un turno respondido o rechazado ya no ofrece ninguna.
 *
 * LA PRESENTACIÓN, EL ALCANCE Y LOS LÍMITES NO ESTÁN ACÁ: viven en el «?» del
 * encabezado —`AyudaDelAsistente`—, que además sigue disponible después del primer
 * turno. Ocupaban la mitad de esta pantalla y competían con lo único accionable que
 * hay: los ejemplos.
 *
 * Desaparece con el primer turno: el panel deja de montarlo.
 */
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
