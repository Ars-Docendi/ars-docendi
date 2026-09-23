import { Button, InlineAlert, Modal } from "@ars-docendi/ui";
import type { SolicitudReservaAula } from "../types";
import { fechaSolicitud } from "./filtrosSolicitudes";

interface ModalCancelarSolicitudProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  solicitud: SolicitudReservaAula | undefined;
  error?: string;
  cancelando?: boolean;
  onConfirmar: () => void;
}

/** Confirmación para cancelar una solicitud propia en estado Pendiente. */
export function ModalCancelarSolicitud({
  open,
  onOpenChange,
  solicitud,
  error,
  cancelando = false,
  onConfirmar,
}: ModalCancelarSolicitudProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Cancelar solicitud"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={cancelando}>
            Volver
          </Button>
          <Button variant="destructive" onClick={onConfirmar} loading={cancelando}>
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
          <strong>{solicitud ? fechaSolicitud(solicitud) : ""}</strong> para{" "}
          <strong>{solicitud?.materia.nombre}</strong> ({solicitud?.comision})?
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
    </Modal>
  );
}
