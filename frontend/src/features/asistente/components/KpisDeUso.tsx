import { formatearEntero, formatearLatenciaMs, formatearUsd } from "../utils/formatoDeUso";
import type { UsoAgregado } from "../types";

interface KpisDeUsoProps {
  /** El agregado organizacional del período elegido; `undefined` mientras carga. */
  organizacion: UsoAgregado | undefined;
}

/**
 * Los cuatro números clave del rediseño «Uso del asistente» (sistema-seccion-
 * unificada): sesiones, llamadas al modelo, costo estimado y latencia p95,
 * TODOS organizacionales y del período que ya eligió el admin más arriba —
 * ni un dato nuevo, sólo lo que `organizacion` de `GET …/uso` ya trae, resumido
 * en tarjetas en vez de la fila «Organización» de la tabla de abajo.
 *
 * EL COSTO SIEMPRE LLEVA «(estimado)»: mismo criterio que la tabla de detalle,
 * la etiqueta va en la tarjeta que muestra el valor, no sólo en un título.
 */
export function KpisDeUso({ organizacion }: KpisDeUsoProps) {
  if (!organizacion) return null;

  const usuariosActivos = organizacion.turnos > 0;
  const porSesion =
    organizacion.turnos > 0 ? organizacion.llamadasAlModelo / organizacion.turnos : 0;

  const tarjetas = [
    {
      etiqueta: "Sesiones",
      valor: formatearEntero(organizacion.turnos),
      nota: usuariosActivos ? "con uso en el período" : "sin uso en este período",
    },
    {
      etiqueta: "Llamadas al modelo",
      valor: formatearEntero(organizacion.llamadasAlModelo),
      nota: `${porSesion.toLocaleString("es-AR", { maximumFractionDigits: 1 })} por sesión`,
    },
    {
      etiqueta: "Costo estimado",
      valor: `${formatearUsd(organizacion.costoEstimado)} (estimado)`,
      nota:
        organizacion.turnosSinPrecio > 0
          ? `${organizacion.turnosSinPrecio} ${organizacion.turnosSinPrecio === 1 ? "turno" : "turnos"} sin precio`
          : "la factura del proveedor manda",
      advertencia: organizacion.turnosSinPrecio > 0,
    },
    {
      etiqueta: "Latencia p95",
      valor: formatearLatenciaMs(organizacion.latenciaP95Ms),
      nota: `promedio ${formatearLatenciaMs(organizacion.latenciaPromedioMs)}`,
    },
  ];

  return (
    <div className="adoc-asistente-admin-kpis">
      {tarjetas.map((t) => (
        <div className="adoc-asistente-admin-kpi" key={t.etiqueta}>
          <span className="adoc-asistente-admin-kpi-etiqueta">{t.etiqueta}</span>
          <span className="adoc-asistente-admin-kpi-valor">{t.valor}</span>
          <span
            className={
              t.advertencia
                ? "adoc-asistente-admin-kpi-nota adoc-asistente-admin-kpi-nota--advertencia"
                : "adoc-asistente-admin-kpi-nota"
            }
          >
            {t.nota}
          </span>
        </div>
      ))}
    </div>
  );
}
