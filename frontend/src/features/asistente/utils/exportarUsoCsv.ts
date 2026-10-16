import type { PeriodoDeUso, UsoAgregado } from "../types";

/**
 * Exportación CSV de la vista activa del detalle de uso (rediseño «Uso del
 * asistente», fidelidad con el canvas de Claude Design: botón «Exportar CSV»
 * en el encabezado). Las columnas son las mismas que ya se ven en la tabla
 * —nunca un dato nuevo, y nunca más PII que el nombre para mostrar que la
 * fila ya trae—, así que no hay nada acá que la pantalla no muestre ya.
 */
const ENCABEZADOS = [
  "Sesiones",
  "Llamadas al modelo",
  "Tokens de entrada",
  "Tokens de salida",
  "Tokens de caché",
  "Latencia promedio (ms)",
  "Latencia p95 (ms)",
  "Costo estimado (USD)",
  "Turnos sin precio",
] as const;

/** Envuelve un campo en comillas dobles sólo si hace falta (RFC 4180). */
function celdaCsv(valor: string | number): string {
  const texto = String(valor);
  if (/[",\n]/.test(texto)) {
    return `"${texto.replace(/"/g, '""')}"`;
  }
  return texto;
}

/**
 * Arma el CSV de una lista de filas ya cargada (por usuario o por rol): no
 * pide nada nuevo al backend, sólo serializa lo que `PanelDeUso` ya tiene en
 * memoria para el período elegido.
 */
export function construirCsvDeUso(filas: UsoAgregado[], esUsuarios: boolean): string {
  const encabezado = [esUsuarios ? "Usuario" : "Rol", ...ENCABEZADOS].map(celdaCsv).join(",");
  const cuerpo = filas.map((f) =>
    [
      f.nombreParaMostrar ?? f.clave,
      f.turnos,
      f.llamadasAlModelo,
      f.tokensDeEntrada,
      f.tokensDeSalida,
      f.tokensDeCache,
      Math.round(f.latenciaPromedioMs),
      Math.round(f.latenciaP95Ms),
      f.costoEstimado.toFixed(2),
      f.turnosSinPrecio,
    ]
      .map(celdaCsv)
      .join(","),
  );
  // BOM UTF-8: Excel en Windows, muy usado en administración, no adivina la
  // codificación de un CSV sin él y rompe los acentos.
  return "﻿" + [encabezado, ...cuerpo].join("\r\n") + "\r\n";
}

/** «uso-asistente-{periodo}-{AAAA-MM-DD}.csv»: identifica período y fecha de exportación. */
export function nombreDeArchivoDeUso(periodo: PeriodoDeUso, fecha: Date = new Date()): string {
  const dia = fecha.toISOString().slice(0, 10);
  return `uso-asistente-${periodo}-${dia}.csv`;
}
