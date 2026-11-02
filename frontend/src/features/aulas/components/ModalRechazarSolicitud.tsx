import { useState } from "react";
import { Button, Field, InlineAlert, Modal, Textarea } from "@ars-docendi/ui";
import type { SolicitudReservaAula } from "../types";
import { fechaSolicitud } from "./filtrosSolicitudes";

interface ModalRechazarSolicitudProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  solicitud: SolicitudReservaAula | undefined;
  error?: string;
  rechazando?: boolean;
  onConfirmar: (motivo: string) => void;
}

/**
 * Rechazo de una solicitud Pendiente (Administrativo), con motivo obligatorio — mismo patrón que
 * "Rechazar pedido" de Designaciones (`ModalConfirmacionAccion`/`PanelAccionesRevision`): el
 * dominio revalida el motivo, este modal solo evita el viaje al backend si está vacío.
 */
export function ModalRechazarSolicitud({
  open,
  onOpenChange,
  solicitud,
  error,
  rechazando = false,
  onConfirmar,
}: ModalRechazarSolicitudProps) {
  const [motivo, setMotivo] = useState("");
  const [mostrarError, setMostrarError] = useState(false);

  function handleConfirmar() {
    if (!motivo.trim()) {
      setMostrarError(true);
      return;
    }
    onConfirmar(motivo.trim());
  }

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Rechazar solicitud"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={rechazando}>
            Volver
          </Button>
          <Button variant="destructive" onClick={handleConfirmar} loading={rechazando}>
            Rechazar solicitud
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo rechazar">
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          Vas a rechazar la solicitud del{" "}
          <strong>{solicitud ? fechaSolicitud(solicitud) : ""}</strong> para{" "}
          <strong>{solicitud?.materia.nombre}</strong> ({solicitud?.comision}).
        </p>
        <Field
          label="Motivo"
          required
          error={mostrarError ? "Indicá el motivo del rechazo." : undefined}
        >
          <Textarea
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="Indicá el motivo del rechazo. El docente lo va a ver completo."
            rows={3}
            invalid={mostrarError && !motivo.trim()}
          />
        </Field>
      </div>
    </Modal>
  );
}
