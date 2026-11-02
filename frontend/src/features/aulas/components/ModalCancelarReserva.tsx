import type { SolicitudReservaAula } from "../types";
import { fechaSolicitud } from "./filtrosSolicitudes";
import { ModalConMotivo } from "./ModalConMotivo";

interface ModalCancelarReservaProps {
  solicitud: SolicitudReservaAula;
  onCerrar: () => void;
  error?: string;
  cancelando?: boolean;
  onConfirmar: (motivo: string) => void;
}

/** Cancelación de una reserva ya aprobada (Administrativo): motivo obligatorio, el aula se libera. */
export function ModalCancelarReserva({
  solicitud,
  onCerrar,
  error,
  cancelando = false,
  onConfirmar,
}: ModalCancelarReservaProps) {
  return (
    <ModalConMotivo
      titulo="Cancelar reserva"
      prefijo="cancelar-reserva"
      etiquetaConfirmar="Cancelar reserva"
      tituloError="No se pudo cancelar la reserva"
      errorMotivoVacio="Indicá el motivo de la cancelación."
      placeholder="Indicá el motivo de la cancelación. El docente lo va a ver completo."
      descripcion={
        <>
          Vas a cancelar la reserva aprobada del <strong>{fechaSolicitud(solicitud)}</strong> para{" "}
          <strong>{solicitud.materia.nombre}</strong> ({solicitud.comision})
          {solicitud.aulaAsignada != null && (
            <>
              {" "}
              en <strong>{solicitud.aulaAsignada}</strong>
            </>
          )}
          .
        </>
      }
      aviso={{
        titulo: "Qué pasa al cancelar",
        texto: "El aula asignada queda liberada y el docente ve el motivo completo.",
      }}
      error={error}
      pendiente={cancelando}
      onCerrar={onCerrar}
      onConfirmar={onConfirmar}
    />
  );
}
