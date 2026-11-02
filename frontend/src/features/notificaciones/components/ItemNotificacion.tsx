import { descripcionAccesible, ENLACE_POR_ENTIDAD } from "../presentacion";
import type { Notificacion } from "../types";
import { IconoCampana, IconoCheck } from "./iconos";
import { ICONO_POR_EVENTO } from "./iconoPorEvento";

interface ItemNotificacionProps {
  notificacion: Notificacion;
  modoSeleccion: boolean;
  seleccionada: boolean;
  onAbrir: (notificacion: Notificacion) => void;
  onAlternar: (notificacion: Notificacion) => void;
}

/** One notification. In selection mode it is a checkbox; otherwise a button that opens its entity. */
export function ItemNotificacion({
  notificacion,
  modoSeleccion,
  seleccionada,
  onAbrir,
  onAlternar,
}: ItemNotificacionProps) {
  const noLeida = notificacion.leidaEn === null;
  const Icono = ICONO_POR_EVENTO[notificacion.tipoEvento] ?? IconoCampana;
  return (
    <button
      type="button"
      data-testid={`notificacion-${notificacion.id}`}
      data-entidad={notificacion.entidadId}
      className={`adoc-notif-item${noLeida ? " no-leida" : ""}`}
      role={modoSeleccion ? "checkbox" : undefined}
      aria-checked={modoSeleccion ? seleccionada : undefined}
      onClick={() => (modoSeleccion ? onAlternar(notificacion) : onAbrir(notificacion))}
    >
      {modoSeleccion && (
        <span className="adoc-notif-check" aria-hidden>
          {seleccionada && <IconoCheck />}
        </span>
      )}
      <span className="adoc-notif-icono">
        <Icono />
      </span>
      <span className="adoc-notif-texto">
        <span className="adoc-notif-titulo">{notificacion.titulo}</span>
        <span className="adoc-notif-mensaje">{notificacion.mensaje}</span>
        <span className="adoc-notif-enlace">
          {ENLACE_POR_ENTIDAD[notificacion.tipoEntidad] ?? "Ir al detalle →"}
        </span>
        <span className="adoc-sr">{descripcionAccesible(notificacion)}</span>
      </span>
    </button>
  );
}
