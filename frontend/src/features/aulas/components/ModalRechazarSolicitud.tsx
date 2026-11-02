import type { SolicitudReservaAula } from "../types";
import { fechaSolicitud } from "./filtrosSolicitudes";
import { ModalConMotivo } from "./ModalConMotivo";

interface ModalRechazarSolicitudProps {
  solicitud: SolicitudReservaAula;
  onCerrar: () => void;
  error?: string;
  rechazando?: boolean;
  onConfirmar: (motivo: string) => void;
}

/**
 * Rechazo de una solicitud Pendiente (Administrativo), con motivo obligatorio. El backend
 * revalida el motivo; este modal sólo evita el viaje si está vacío.
 */
export function ModalRechazarSolicitud({
  solicitud,
  onCerrar,
  error,
  rechazando = false,
  onConfirmar,
}: ModalRechazarSolicitudProps) {
  return (
    <ModalConMotivo
      titulo="Rechazar solicitud"
      prefijo="rechazar"
      etiquetaConfirmar="Rechazar solicitud"
      tituloError="No se pudo rechazar"
      errorMotivoVacio="Indicá el motivo del rechazo."
      placeholder="Indicá el motivo del rechazo. El docente lo va a ver completo."
      descripcion={
        <>
          Vas a rechazar la solicitud del <strong>{fechaSolicitud(solicitud)}</strong> para{" "}
          <strong>{solicitud.materia.nombre}</strong> ({solicitud.comision}).
        </>
      }
      error={error}
      pendiente={rechazando}
      onCerrar={onCerrar}
      onConfirmar={onConfirmar}
    />
  );
}
