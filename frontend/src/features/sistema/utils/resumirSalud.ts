import type { MantenimientoAsistente } from "../api/sistemaApi";

export type VarianteBanner = "ok" | "fallo" | "mantenimiento" | "pendiente";
export type EstadoDePunto = "positivo" | "negativo" | "advertencia" | "neutral";

export interface EntradaComponenteSalud {
  id: string;
  nombre: string;
  /** `undefined` mientras la consulta todavía no terminó (pendiente). */
  disponible: boolean | undefined;
  comprobadoEn?: string;
}

export interface EntradaResumenSalud {
  /** Los seis componentes: cinco pings más PostgreSQL (design D12). */
  componentes: EntradaComponenteSalud[];
  mantenimientoAsistente: MantenimientoAsistente;
}

export interface ResumenSalud {
  variante: VarianteBanner;
  titulo: string;
  subtitulo: string;
  totalDisponibles: number;
  totalComponentes: number;
  nombresNoDisponibles: string[];
  ultimaComprobacion: string | null;
  puntoEstado: EstadoDePunto;
  puntoAsistente: EstadoDePunto;
}

function ultimaComprobacion(componentes: EntradaComponenteSalud[]): string | null {
  const fechas = componentes
    .map((c) => c.comprobadoEn)
    .filter((v): v is string => Boolean(v))
    .map((v) => new Date(v).getTime())
    .filter((t) => Number.isFinite(t));
  if (fechas.length === 0) return null;
  return new Date(Math.max(...fechas)).toISOString();
}

/**
 * Resumen puro de salud (design D12, sistema-seccion-unificada): banner,
 * nombres de componentes caídos y estado de los dos puntos que dependen de
 * la salud (Estado y Asistente). La indisponibilidad SIEMPRE pesa más que el
 * mantenimiento (requisito "Estado summary banner..."): un componente caído
 * nunca se pisa con el aviso de mantenimiento.
 */
export function resumirSalud({
  componentes,
  mantenimientoAsistente,
}: EntradaResumenSalud): ResumenSalud {
  const pendiente = componentes.some((c) => c.disponible === undefined);
  const noDisponibles = componentes.filter((c) => c.disponible === false);
  const totalDisponibles = componentes.filter((c) => c.disponible === true).length;
  const totalComponentes = componentes.length;
  const nombresNoDisponibles = noDisponibles.map((c) => c.nombre);
  const ultima = ultimaComprobacion(componentes);

  const puntoEstado: EstadoDePunto = pendiente
    ? "neutral"
    : noDisponibles.length > 0
      ? "negativo"
      : "positivo";
  const puntoAsistente: EstadoDePunto =
    mantenimientoAsistente === "desconocido"
      ? "neutral"
      : mantenimientoAsistente === "activo"
        ? "advertencia"
        : "positivo";

  if (pendiente) {
    return {
      variante: "pendiente",
      titulo: "Comprobando componentes…",
      subtitulo: "",
      totalDisponibles,
      totalComponentes,
      nombresNoDisponibles,
      ultimaComprobacion: ultima,
      puntoEstado,
      puntoAsistente,
    };
  }

  if (noDisponibles.length > 0) {
    const cantidad = noDisponibles.length;
    const titulo =
      cantidad === 1 ? "1 componente no disponible" : `${cantidad} componentes no disponibles`;
    const listado = new Intl.ListFormat("es-AR", { type: "conjunction" }).format(
      nombresNoDisponibles,
    );
    const restoDisponible = totalDisponibles > 0;
    const sujeto = cantidad === 1 ? "no respondió" : "no respondieron";
    const subtitulo = restoDisponible
      ? `${listado} ${sujeto}. El resto funciona con normalidad.`
      : `${listado} ${sujeto}.`;
    return {
      variante: "fallo",
      titulo,
      subtitulo,
      totalDisponibles,
      totalComponentes,
      nombresNoDisponibles,
      ultimaComprobacion: ultima,
      puntoEstado,
      puntoAsistente,
    };
  }

  if (mantenimientoAsistente === "activo") {
    return {
      variante: "mantenimiento",
      titulo: "Todo disponible, asistente en mantenimiento",
      subtitulo:
        "Los módulos responden. El asistente no acepta consultas hasta que se desactive el mantenimiento.",
      totalDisponibles,
      totalComponentes,
      nombresNoDisponibles,
      ultimaComprobacion: ultima,
      puntoEstado,
      puntoAsistente,
    };
  }

  return {
    variante: "ok",
    titulo: "Todos los componentes disponibles",
    subtitulo: `${totalDisponibles} de ${totalComponentes} responden con normalidad.`,
    totalDisponibles,
    totalComponentes,
    nombresNoDisponibles,
    ultimaComprobacion: ultima,
    puntoEstado,
    puntoAsistente,
  };
}
