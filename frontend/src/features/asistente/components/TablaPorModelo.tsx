import { useId } from "react";

import type { ModeloDeCalidad } from "../types";
import { formatearCantidad, formatearTasa } from "../utils/calidad";

interface TablaPorModeloProps {
  filas: ModeloDeCalidad[];
}

/** Per-model comparison. The turn count is there because rates are only fair at similar volume. */
export function TablaPorModelo({ filas }: TablaPorModeloProps) {
  const idTitulo = useId();
  return (
    <section className="adoc-asistente-admin-panel-uso-tarjeta" aria-labelledby={idTitulo}>
      <span id={idTitulo} className="sistema-cambios-titulo">
        Por modelo
      </span>
      {filas.length === 0 ? (
        <p className="adoc-asistente-admin-panel-vacio-texto">No hay turnos en este período.</p>
      ) : (
        <div className="adoc-table-wrap">
          <table className="adoc-table">
            <thead>
              <tr>
                <th scope="col">Modelo</th>
                <th scope="col" className="calidad-numero">
                  Turnos
                </th>
                <th scope="col" className="calidad-numero">
                  Negativas
                </th>
                <th scope="col" className="calidad-numero">
                  Fallos
                </th>
                <th scope="col" className="calidad-numero">
                  Con problemas
                </th>
              </tr>
            </thead>
            <tbody>
              {filas.map((f) => (
                <tr key={f.modelo ?? "sin-modelo"}>
                  <td className="calidad-mono">{f.modelo ?? "Sin registrar"}</td>
                  <td className="calidad-numero">{formatearCantidad(f.resumen.turnos)}</td>
                  <td className="calidad-numero">{formatearTasa(f.resumen.tasaNegativa)}</td>
                  <td className="calidad-numero">{formatearTasa(f.resumen.tasaDeFallos)}</td>
                  <td className="calidad-numero">{formatearTasa(f.resumen.tasaConProblemas)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
