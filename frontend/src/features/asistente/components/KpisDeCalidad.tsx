import type { CalidadAgregada } from "../types";
import {
  TEXTO_SIN_TENDENCIA,
  formatearCantidad,
  formatearPuntos,
  formatearTasa,
} from "../utils/calidad";

interface KpisDeCalidadProps {
  calidad: CalidadAgregada;
}

/** The trend note of the headline, and whether it must be flagged as rising. */
function notaDeTendencia(calidad: CalidadAgregada): { texto: string; enAlza: boolean } {
  const { tendencia, motivoSinTendencia } = calidad;
  if (!tendencia) {
    return {
      texto: motivoSinTendencia
        ? TEXTO_SIN_TENDENCIA[motivoSinTendencia]
        : "Sin comparación disponible",
      enAlza: false,
    };
  }
  if (tendencia.variacion > 0) {
    return {
      texto: `▲ ${formatearPuntos(tendencia.variacion)} pts contra el período anterior`,
      enAlza: true,
    };
  }
  if (tendencia.variacion < 0) {
    return {
      texto: `▼ ${formatearPuntos(tendencia.variacion)} pts contra el período anterior`,
      enAlza: false,
    };
  }
  return { texto: "Sin cambios contra el período anterior", enAlza: false };
}

/**
 * The quality KPI row. «Respuestas negativas» is first, wider and red-accented (it is the
 * headline); the rest reuse the usage panel's card styles.
 */
export function KpisDeCalidad({ calidad }: KpisDeCalidadProps) {
  const { resumen } = calidad;
  const tendencia = notaDeTendencia(calidad);
  const tasaPositiva = resumen.votos === 0 ? null : resumen.positivos / resumen.votos;

  return (
    <section
      aria-label="Indicadores de calidad"
      className="adoc-asistente-admin-kpis-fila calidad-kpis"
    >
      <div className="adoc-asistente-admin-kpi calidad-kpi--negativa">
        <span className="adoc-asistente-admin-kpi-etiqueta">Respuestas negativas</span>
        <span className="adoc-asistente-admin-kpi-valor-linea">
          <span>{formatearTasa(resumen.tasaNegativa)}</span>
          <span className="adoc-asistente-admin-kpi-prefijo">de las valoradas</span>
        </span>
        <span className="adoc-asistente-admin-kpi-nota">
          {formatearCantidad(resumen.negativos)} de {formatearCantidad(resumen.votos)} valoraciones
        </span>
        <span
          className={
            tendencia.enAlza
              ? "adoc-asistente-admin-kpi-nota adoc-asistente-admin-kpi-nota--advertencia"
              : "adoc-asistente-admin-kpi-nota"
          }
          data-testid="tendencia-negativas"
        >
          {tendencia.texto}
        </span>
      </div>
      <div className="adoc-asistente-admin-kpi">
        <span className="adoc-asistente-admin-kpi-etiqueta">Con problemas</span>
        <span className="adoc-asistente-admin-kpi-valor-linea">
          <span>{formatearTasa(resumen.tasaConProblemas)}</span>
        </span>
        <span className="adoc-asistente-admin-kpi-nota">
          Negativas, fallos o reemplazadas, sobre {formatearCantidad(resumen.turnos)} turnos
        </span>
      </div>
      <div className="adoc-asistente-admin-kpi">
        <span className="adoc-asistente-admin-kpi-etiqueta">Fallos detectables</span>
        <span className="adoc-asistente-admin-kpi-valor-linea">
          <span>{formatearTasa(resumen.tasaDeFallos)}</span>
        </span>
        <span className="adoc-asistente-admin-kpi-nota">
          {formatearCantidad(resumen.fallosDetectables)} de {formatearCantidad(resumen.turnos)}{" "}
          turnos
        </span>
      </div>
      <div className="adoc-asistente-admin-kpi">
        <span className="adoc-asistente-admin-kpi-etiqueta">Positivas</span>
        <span className="adoc-asistente-admin-kpi-valor-linea">
          <span>{formatearTasa(tasaPositiva)}</span>
        </span>
        <span className="adoc-asistente-admin-kpi-nota">
          {formatearCantidad(resumen.positivos)}{" "}
          {resumen.positivos === 1 ? "pulgar arriba" : "pulgares arriba"}
        </span>
      </div>
      <div className="adoc-asistente-admin-kpi">
        <span className="adoc-asistente-admin-kpi-etiqueta">Cobertura de voto</span>
        <span className="adoc-asistente-admin-kpi-valor-linea">
          <span>{formatearTasa(resumen.cobertura)}</span>
        </span>
        <span className="adoc-asistente-admin-kpi-nota">Respuestas que alguien valoró</span>
      </div>
    </section>
  );
}
