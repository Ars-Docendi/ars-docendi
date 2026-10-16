import { formatearEntero, formatearLatenciaMs } from "../utils/formatoDeUso";
import type { UsoAgregado } from "../types";

interface KpisDeUsoProps {
  /** El agregado organizacional del período elegido; `undefined` mientras carga. */
  organizacion: UsoAgregado | undefined;
  /**
   * Cuántos usuarios tuvieron al menos un turno en el período —«N usuarios
   * activos» del canvas (fidelidad «Uso del asistente»)—, derivado en el
   * padre de `uso.porUsuario`: el agregado organizacional no lo trae solo.
   * `undefined` mientras `uso` todavía no llegó.
   */
  usuariosActivos: number | undefined;
}

/** Separa el prefijo «US$» del número: mismo patrón que el canvas, dos nodos
 * de texto adentro de un mismo contenedor para que la tipografía del prefijo
 * sea más chica que la del número. */
function ValorConPrefijo({ prefijo, valor }: { prefijo?: string; valor: string }) {
  return (
    <span className="adoc-asistente-admin-kpi-valor-linea">
      {prefijo && <span className="adoc-asistente-admin-kpi-prefijo">{prefijo}</span>}
      <span>{valor}</span>
    </span>
  );
}

/**
 * Los cuatro números clave del rediseño «Uso del asistente» (sistema-seccion-
 * unificada), con la tipografía y las etiquetas 1:1 con el canvas de Claude
 * Design: SESIONES, LLAMADAS, COSTO ESTIMADO (prefijo «US$» más chico que el
 * número) y LATENCIA P95 — TODOS organizacionales y del período que ya eligió
 * el admin más arriba, ni un dato nuevo, sólo lo que `organizacion` de
 * `GET …/uso` ya trae, resumido en tarjetas en vez de la fila «Organización»
 * de la tabla de abajo.
 *
 * EL COSTO SIEMPRE LLEVA «(estimado)»: mismo criterio que la tabla de detalle,
 * la nota de la tarjeta lo aclara aunque el prefijo ya diga «US$».
 */
export function KpisDeUso({ organizacion, usuariosActivos }: KpisDeUsoProps) {
  if (!organizacion) return null;

  const porSesion =
    organizacion.turnos > 0 ? organizacion.llamadasAlModelo / organizacion.turnos : 0;

  const tarjetas = [
    {
      etiqueta: "Sesiones",
      valor: <ValorConPrefijo valor={formatearEntero(organizacion.turnos)} />,
      nota:
        usuariosActivos === undefined
          ? ""
          : `${formatearEntero(usuariosActivos)} ${usuariosActivos === 1 ? "usuario activo" : "usuarios activos"}`,
    },
    {
      etiqueta: "Llamadas",
      valor: <ValorConPrefijo valor={formatearEntero(organizacion.llamadasAlModelo)} />,
      nota: `${porSesion.toLocaleString("es-AR", { maximumFractionDigits: 1 })} por sesión`,
    },
    {
      etiqueta: "Costo estimado",
      valor: (
        <ValorConPrefijo
          prefijo="US$"
          valor={organizacion.costoEstimado.toLocaleString("es-AR", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
          })}
        />
      ),
      nota:
        organizacion.turnosSinPrecio > 0
          ? `${organizacion.turnosSinPrecio} ${organizacion.turnosSinPrecio === 1 ? "turno" : "turnos"} sin precio`
          : "Estimado; manda la factura.",
      advertencia: organizacion.turnosSinPrecio > 0,
    },
    {
      etiqueta: "Latencia p95",
      valor: <ValorConPrefijo valor={formatearLatenciaMs(organizacion.latenciaP95Ms)} />,
      nota: `Promedio ${formatearLatenciaMs(organizacion.latenciaPromedioMs)}`,
    },
  ];

  return (
    <div className="adoc-asistente-admin-kpis">
      {tarjetas.map((t) => (
        <div className="adoc-asistente-admin-kpi" key={t.etiqueta}>
          <span className="adoc-asistente-admin-kpi-etiqueta">{t.etiqueta}</span>
          {t.valor}
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
