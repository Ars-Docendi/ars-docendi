import { useResaltado } from "../../../shared/ui/resaltado";
import type { SolicitudReservaAula } from "../types";
import { EstadoSolicitudPill } from "./EstadoSolicitudPill";
import { fechaSolicitud, horarioSolicitud } from "./filtrosSolicitudes";

/** Tarjeta "Datos de la solicitud": todos los campos y, según el estado, el motivo de cierre. */
export function DatosSolicitud({ solicitud }: { solicitud: SolicitudReservaAula }) {
  const resaltada = useResaltado(solicitud.id);
  const docente = solicitud.docente
    ? `${solicitud.docente.apellido}, ${solicitud.docente.nombre}`
    : "—";
  return (
    <section
      className={`adoc-aulas-card adoc-aulas-panel${resaltada ? " adoc-resaltado" : ""}`}
      aria-label="Datos de la solicitud"
      data-entidad-id={solicitud.id}
    >
      <h2>Datos de la solicitud</h2>
      <dl className="adoc-aulas-dl">
        <dt>Estado</dt>
        <dd>
          <EstadoSolicitudPill estado={solicitud.estado} />
        </dd>
        <dt>Docente</dt>
        <dd>{docente}</dd>
        <dt>Día</dt>
        <dd>{fechaSolicitud(solicitud)}</dd>
        <dt>Horario</dt>
        <dd>{horarioSolicitud(solicitud)}</dd>
        <dt>Cód. Materia</dt>
        <dd className="adoc-aulas-codigo">{solicitud.materia.codigo}</dd>
        <dt>Materia</dt>
        <dd>{solicitud.materia.nombre}</dd>
        <dt>Comisión</dt>
        <dd>{solicitud.comision}</dd>
        <dt>Capacidad</dt>
        <dd>{solicitud.cantidadAlumnosAprox}</dd>
        <dt>Aula</dt>
        <dd>{solicitud.aulaAsignada ?? "—"}</dd>
        {solicitud.motivoRechazo != null && (
          <>
            <dt>Motivo del rechazo</dt>
            <dd>{solicitud.motivoRechazo}</dd>
          </>
        )}
        {solicitud.motivoCancelacion != null && (
          <>
            <dt>Motivo de la cancelación</dt>
            <dd>{solicitud.motivoCancelacion}</dd>
          </>
        )}
      </dl>
    </section>
  );
}
