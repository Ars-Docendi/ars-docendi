// ============================================================
// Semáforo de vencimiento — LÓGICA PURA. El umbral es relativo a la
// duración de la tarea (Fecha Inicio → Fecha Fin), no un número fijo
// de días: verde <50% transcurrido, amarillo 50-80%, rojo ≥80%
// (incluida vencida). Solo aplica a estados no terminales.
// ============================================================
import type { TrafficState } from "@ars-docendi/ui";
import type { EstadoTarea } from "../types";

const ESTADOS_NO_TERMINALES: readonly EstadoTarea[] = ["pendiente", "en_curso", "pausa"];

/** ¿Corresponde mostrar semáforo para este estado? Resuelta/Cancelada no lo muestran. */
export function muestraSemaforo(estado: EstadoTarea): boolean {
  return ESTADOS_NO_TERMINALES.includes(estado);
}

/** Porcentaje del plazo transcurrido entre fechaInicio y fechaFin, relativo a `hoy`. */
export function porcentajeTranscurrido(
  fechaInicio: string,
  fechaFin: string,
  hoy: Date = new Date(),
): number {
  const inicio = new Date(fechaInicio).getTime();
  const fin = new Date(fechaFin).getTime();
  const totalDias = fin - inicio;
  if (totalDias <= 0) return 100;
  const transcurridos = hoy.getTime() - inicio;
  return (transcurridos / totalDias) * 100;
}

/** Estado del semáforo (verde/amarillo/rojo) según el % del plazo transcurrido. */
export function estadoSemaforo(
  fechaInicio: string,
  fechaFin: string,
  hoy: Date = new Date(),
): TrafficState {
  const porcentaje = porcentajeTranscurrido(fechaInicio, fechaFin, hoy);
  if (porcentaje >= 80) return "red";
  if (porcentaje >= 50) return "yellow";
  return "green";
}
