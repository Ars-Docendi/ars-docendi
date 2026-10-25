import { useId, useState } from "react";

import type { CalidadAgregada } from "../types";
import {
  formatearCantidad,
  formatearDia,
  formatearTasa,
  maximoDeEscala,
  textoDeBarra,
} from "../utils/calidad";

interface GraficoDeNegativasProps {
  calidad: CalidadAgregada;
}

/**
 * Negative percentage per day (per week for 90 days): one series, so no legend;
 * thin bars with rounded tops, an average reference line and a recessive axis.
 *
 * The bars are decorative for assistive technology: the container is an `img`
 * with a summary label, and «Ver como tabla» shows the same numbers as a table.
 * The hover tooltip is a convenience, never the only way to read a value.
 */
export function GraficoDeNegativas({ calidad }: GraficoDeNegativasProps) {
  const [comoTabla, setComoTabla] = useState(false);
  const idTitulo = useId();
  const idTabla = useId();
  const { serie, porSemana, resumen } = calidad;
  const unidad = porSemana ? "semana" : "día";

  const promedio = resumen.tasaNegativa;
  const maximo = maximoDeEscala(serie);
  const alturaPromedio = promedio === null ? null : Math.min(100, (promedio * 100 * 100) / maximo);
  const desde = serie.length > 0 ? formatearDia(serie[0].dia) : formatearDia(calidad.desde);
  const hasta =
    serie.length > 0 ? formatearDia(serie[serie.length - 1].dia) : formatearDia(calidad.hasta);
  const descripcion = `Porcentaje de respuestas negativas por ${unidad}, del ${desde} al ${hasta}. Promedio ${formatearTasa(promedio)}.`;

  return (
    <section className="adoc-asistente-admin-panel-uso-tarjeta" aria-labelledby={idTitulo}>
      <div className="adoc-asistente-admin-panel-uso-encabezado">
        <span id={idTitulo} className="sistema-cambios-titulo">
          Respuestas negativas por {unidad}
        </span>
        <span className="calidad-grafico-encabezado-derecha">
          <span className="adoc-hint calidad-sin-margen">Promedio {formatearTasa(promedio)}</span>
          {serie.length > 0 && (
            <button
              type="button"
              className="calidad-enlace"
              aria-expanded={comoTabla}
              aria-controls={idTabla}
              onClick={() => setComoTabla((v) => !v)}
            >
              {comoTabla ? "Ver como gráfico" : "Ver como tabla"}
            </button>
          )}
        </span>
      </div>

      {serie.length === 0 ? (
        <p className="adoc-asistente-admin-panel-vacio-texto">No hay turnos en este período.</p>
      ) : (
        <div id={idTabla}>
          {comoTabla ? (
            <div className="adoc-table-wrap">
              <table className="adoc-table">
                <caption className="calidad-solo-lector">{descripcion}</caption>
                <thead>
                  <tr>
                    <th scope="col">{porSemana ? "Semana del" : "Día"}</th>
                    <th scope="col" className="calidad-numero">
                      Valoraciones
                    </th>
                    <th scope="col" className="calidad-numero">
                      Negativas
                    </th>
                    <th scope="col" className="calidad-numero">
                      % negativas
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {serie.map((p) => (
                    <tr key={p.dia}>
                      <td>{formatearDia(p.dia)}</td>
                      <td className="calidad-numero">{formatearCantidad(p.votos)}</td>
                      <td className="calidad-numero">{formatearCantidad(p.negativos)}</td>
                      <td className="calidad-numero">{formatearTasa(p.tasaNegativa)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <>
              <div className="calidad-grafico" role="img" aria-label={descripcion}>
                <span className="calidad-grafico-max" aria-hidden="true">
                  {maximo}%
                </span>
                {alturaPromedio !== null && (
                  <span
                    className="calidad-grafico-promedio"
                    aria-hidden="true"
                    style={{ bottom: `${alturaPromedio}%` }}
                  />
                )}
                {serie.map((p) => (
                  <span className="calidad-barra-celda" aria-hidden="true" key={p.dia}>
                    <span
                      className="calidad-barra"
                      style={{
                        height: `${Math.max(1, ((p.tasaNegativa ?? 0) * 100 * 100) / maximo)}%`,
                      }}
                    />
                    <span className="calidad-barra-tip">{textoDeBarra(p, porSemana)}</span>
                  </span>
                ))}
              </div>
              <div className="calidad-grafico-eje" aria-hidden="true">
                <span>{desde}</span>
                <span>{hasta}</span>
              </div>
            </>
          )}
        </div>
      )}
    </section>
  );
}
