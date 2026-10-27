import { Button } from "@ars-docendi/ui";

import type { MantenimientoAsistente, MotivoFalla } from "../api/sistemaApi";

export type TipoDeComponente = "modulo" | "asistente" | "postgresql" | "inferencia";
export type EstadoDeTarjeta =
  "disponible" | "no_disponible" | "mantenimiento" | "deshabilitado" | "cargando";

interface TarjetaDeComponenteProps {
  nombre: string;
  tipo: TipoDeComponente;
  cargando: boolean;
  disponible?: boolean;
  /** Apagado por configuración del ambiente: estado neutral, no una falla. */
  deshabilitado?: boolean;
  motivoFalla?: MotivoFalla;
  duracionMs?: number;
  mantenimientoAsistente?: MantenimientoAsistente;
  puedeVerUso?: boolean;
  onReintentar: () => void;
  onVerUso?: () => void;
}

function calcularEstado(
  tipo: TipoDeComponente,
  cargando: boolean,
  disponible: boolean | undefined,
  deshabilitado: boolean,
  mantenimientoAsistente: MantenimientoAsistente | undefined,
): EstadoDeTarjeta {
  if (cargando || disponible === undefined) return "cargando";
  if (deshabilitado) return "deshabilitado";
  if (!disponible) return "no_disponible";
  if (tipo === "asistente" && mantenimientoAsistente === "activo") return "mantenimiento";
  return "disponible";
}

const ETIQUETA_PILL: Record<EstadoDeTarjeta, string> = {
  disponible: "Disponible",
  no_disponible: "No disponible",
  mantenimiento: "Mantenimiento",
  deshabilitado: "Deshabilitado",
  cargando: "Comprobando…",
};

function calcularNota(
  tipo: TipoDeComponente,
  estado: EstadoDeTarjeta,
  motivoFalla: MotivoFalla | undefined,
): string {
  if (estado === "cargando") return "Comprobando…";
  if (estado === "deshabilitado") return "No configurada en este ambiente";
  if (tipo === "postgresql" || tipo === "inferencia")
    return estado === "disponible"
      ? "Comprobado por separado"
      : tipo === "postgresql"
        ? "No se pudo comprobar la conexión."
        : "No se pudo comprobar el servicio.";
  if (estado === "mantenimiento") return "Consultas pausadas por un administrador";
  if (estado === "no_disponible")
    return motivoFalla === "timeout" ? "No respondió en 5 s" : "Respondió con error";
  return tipo === "asistente" ? "Recibe consultas" : "Responde normalmente";
}

/** Una tarjeta del grupo «Módulos» o «Infraestructura» (design D12, requisito «Estado summary banner...»). */
export function TarjetaDeComponente({
  nombre,
  tipo,
  cargando,
  disponible,
  deshabilitado = false,
  motivoFalla,
  duracionMs,
  mantenimientoAsistente,
  puedeVerUso,
  onReintentar,
  onVerUso,
}: TarjetaDeComponenteProps) {
  const estado = calcularEstado(tipo, cargando, disponible, deshabilitado, mantenimientoAsistente);
  const nota = calcularNota(tipo, estado, motivoFalla);
  const muestraDuracion = estado === "disponible" || estado === "mantenimiento";

  return (
    <div className={`sistema-tarjeta sistema-tarjeta--${estado}`}>
      <div className="sistema-tarjeta-encabezado">
        <span className="sistema-tarjeta-nombre">{nombre}</span>
        <span className={`sistema-pill sistema-pill--${estado}`}>
          <span aria-hidden="true" className="sistema-pill-punto" />
          {ETIQUETA_PILL[estado]}
        </span>
      </div>
      <div className="sistema-tarjeta-duracion">
        {muestraDuracion && duracionMs !== undefined ? (
          <>
            <span className="sistema-tarjeta-ms">{Math.round(duracionMs)} ms</span>
            <span className="sistema-tarjeta-ms-label">tiempo de respuesta</span>
          </>
        ) : estado === "no_disponible" ? (
          <span className="sistema-tarjeta-ms-label">sin respuesta</span>
        ) : (
          <span className="sistema-tarjeta-ms-label" aria-hidden="true">
            —
          </span>
        )}
      </div>
      <div className="sistema-tarjeta-pie">
        <span className={`sistema-tarjeta-nota sistema-tarjeta-nota--${estado}`}>{nota}</span>
        {tipo === "asistente" && puedeVerUso && (
          <Button variant="ghost" size="sm" type="button" onClick={onVerUso}>
            Ver uso →
          </Button>
        )}
        {estado === "no_disponible" && (
          <Button variant="secondary" size="sm" type="button" onClick={onReintentar}>
            Reintentar
          </Button>
        )}
      </div>
    </div>
  );
}
