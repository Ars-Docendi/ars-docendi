import { Button, InlineAlert } from "@ars-docendi/ui";
import { ModalDeFormulario } from "../../../shared/ui/ModalDeFormulario";
import type { SolicitudReservaAula } from "../types";
import { fechaSolicitud } from "./filtrosSolicitudes";

interface ModalCancelarSolicitudProps {
  solicitud: SolicitudReservaAula;
  onCerrar: () => void;
  error?: string;
  cancelando?: boolean;
  onConfirmar: () => void;
}

/** Confirmación para cancelar una solicitud propia en estado Pendiente. */
export function ModalCancelarSolicitud({
  solicitud,
  onCerrar,
  error,
  cancelando = false,
  onConfirmar,
}: ModalCancelarSolicitudProps) {
  return (
    <ModalDeFormulario
      titulo="Cancelar solicitud"
      onCerrar={onCerrar}
      pendiente={cancelando}
      pie={
        <>
          <Button id="cancelar-volver" variant="secondary" onClick={onCerrar} disabled={cancelando}>
            Volver
          </Button>
          <Button
            id="cancelar-confirmar"
            variant="destructive"
            onClick={onConfirmar}
            loading={cancelando}
          >
            Cancelar solicitud
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo cancelar">
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          ¿Estás seguro de que querés cancelar la solicitud del{" "}
          <strong>{fechaSolicitud(solicitud)}</strong> para{" "}
          <strong>{solicitud.materia.nombre}</strong> ({solicitud.comision})?
        </p>
        <p
          style={{
            margin: 0,
            fontSize: "var(--text-body-sm-size)",
            color: "var(--color-text-secondary)",
          }}
        >
          Esta acción no se puede deshacer.
        </p>
      </div>
    </ModalDeFormulario>
  );
}
