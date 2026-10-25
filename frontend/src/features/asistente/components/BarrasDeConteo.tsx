import { useId, type ReactNode } from "react";

import type { ConteoDeCalidad } from "../types";
import { esSenal, formatearCantidad, formatearTasa } from "../utils/calidad";

interface BarrasDeConteoProps {
  titulo: string;
  conteos: ConteoDeCalidad[];
  etiquetaDe: (clave: string) => string;
  vacio: string;
  /** A note under the title (e.g. that a vote can carry several reasons). */
  nota?: ReactNode;
  /** Marks `resultado_vacio` as a signal. Only meaningful for causes. */
  marcarSenales?: boolean;
}

/**
 * Horizontal bars of a breakdown, largest first. Each row shows its count and
 * its share of the listed total; the bar is proportional to the largest row.
 */
export function BarrasDeConteo({
  titulo,
  conteos,
  etiquetaDe,
  vacio,
  nota,
  marcarSenales = false,
}: BarrasDeConteoProps) {
  const idTitulo = useId();
  const ordenados = [...conteos].sort((a, b) => b.cantidad - a.cantidad);
  const total = ordenados.reduce((suma, c) => suma + c.cantidad, 0);
  const mayor = Math.max(1, ...ordenados.map((c) => c.cantidad));

  return (
    <section className="adoc-asistente-admin-panel-uso-tarjeta" aria-labelledby={idTitulo}>
      <span id={idTitulo} className="sistema-cambios-titulo">
        {titulo}
      </span>
      {nota && <p className="adoc-hint calidad-sin-margen">{nota}</p>}
      {ordenados.length === 0 ? (
        <p className="adoc-asistente-admin-panel-vacio-texto">{vacio}</p>
      ) : (
        <ul className="calidad-lista-barras">
          {ordenados.map((c) => (
            <li className="calidad-fila-barra" key={c.clave}>
              <span className="calidad-fila-etiqueta">
                {etiquetaDe(c.clave)}
                {marcarSenales && esSenal(c) && (
                  <span
                    className="calidad-senal"
                    title="Una consulta sin resultados suele ser una respuesta correcta: no cuenta como fallo."
                  >
                    Señal
                  </span>
                )}
              </span>
              <span className="adoc-asistente-admin-metrica-celda">
                <span className="adoc-asistente-admin-metrica-pista" aria-hidden="true">
                  <span
                    className="adoc-asistente-admin-metrica-barra"
                    style={{ width: `${(c.cantidad / mayor) * 100}%` }}
                  />
                </span>
                <span className="adoc-asistente-admin-metrica-texto">
                  {formatearCantidad(c.cantidad)} ·{" "}
                  {formatearTasa(total === 0 ? null : c.cantidad / total)}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
