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

/** La tarea vence al terminar su fecha de fin, en el día local: el día de vencimiento todavía no está vencida. */
function estaVencida(fechaFin: string, hoy: Date): boolean {
  const mes = String(hoy.getMonth() + 1).padStart(2, "0");
  const dia = String(hoy.getDate()).padStart(2, "0");
  return `${hoy.getFullYear()}-${mes}-${dia}` > fechaFin.slice(0, 10);
}

/** Texto del semáforo: el rojo avisa que queda poco plazo, y sólo es "Vencido" si la fecha ya pasó. */
export function etiquetaSemaforo(
  fechaInicio: string,
  fechaFin: string,
  hoy: Date = new Date(),
): string {
  if (estaVencida(fechaFin, hoy)) return "Vencido";
  return estadoSemaforo(fechaInicio, fechaFin, hoy) === "green" ? "A tiempo" : "Próximo a vencer";
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

/**
 * Nivel de alerta que tiñe una fila o tarjeta: solo amarillo (por vencer) y rojo (vencida)
 * resaltan; verde es el caso normal y los estados terminales no muestran semáforo.
 */
export function nivelAlertaSemaforo(
  estado: EstadoTarea,
  fechaInicio: string,
  fechaFin: string,
): "por-vencer" | "vencida" | null {
  if (!muestraSemaforo(estado)) return null;
  const semaforo = estadoSemaforo(fechaInicio, fechaFin);
  return semaforo === "red" ? "vencida" : semaforo === "yellow" ? "por-vencer" : null;
}
