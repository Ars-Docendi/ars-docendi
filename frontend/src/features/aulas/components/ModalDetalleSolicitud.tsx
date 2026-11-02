import { Button, Modal } from "@ars-docendi/ui";
import type { SolicitudReservaAula } from "../types";
import { EstadoSolicitudPill } from "./EstadoSolicitudPill";
import { fechaSolicitud, horarioSolicitud } from "./filtrosSolicitudes";

interface ModalDetalleSolicitudProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  solicitud: SolicitudReservaAula | undefined;
}

const FILA = { display: "flex", justifyContent: "space-between", gap: "var(--space-4)" } as const;

/**
 * Popup de solo lectura para una fila `Rechazada` de "Mis solicitudes": el Docente dueño ve todos
 * los datos de su solicitud y el motivo del rechazo. No ofrece ninguna acción, solo "Cerrar".
 */
export function ModalDetalleSolicitud({
  open,
  onOpenChange,
  solicitud,
}: ModalDetalleSolicitudProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Solicitud rechazada"
      footer={
        <Button variant="secondary" onClick={() => onOpenChange(false)}>
          Cerrar
        </Button>
      }
    >
      {solicitud && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Estado</span>
            <EstadoSolicitudPill estado={solicitud.estado} />
          </div>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Día</span>
            <strong>{fechaSolicitud(solicitud)}</strong>
          </div>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Horario</span>
            <strong>{horarioSolicitud(solicitud)}</strong>
          </div>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Cód. Materia</span>
            <strong>{solicitud.materia.codigo}</strong>
          </div>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Materia</span>
            <strong>{solicitud.materia.nombre}</strong>
          </div>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Comisión</span>
            <strong>{solicitud.comision}</strong>
          </div>
          <div style={FILA}>
            <span style={{ color: "var(--color-text-secondary)" }}>Capacidad</span>
            <strong>{solicitud.cantidadAlumnosAprox}</strong>
          </div>
          <div
            style={{
              borderTop: "1px solid var(--color-border-default)",
              paddingTop: "var(--space-3)",
            }}
          >
            <span
              style={{
                display: "block",
                color: "var(--color-text-secondary)",
                marginBottom: "4px",
              }}
            >
              Motivo del rechazo
            </span>
            <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
              {solicitud.motivoRechazo}
            </p>
          </div>
        </div>
      )}
    </Modal>
  );
}
