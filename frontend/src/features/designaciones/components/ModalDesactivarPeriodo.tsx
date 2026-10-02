import { Modal, Button, InlineAlert } from "@ars-docendi/ui";
import type { PeriodoDesignacion } from "../types";

interface ModalDesactivarPeriodoProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  periodo: PeriodoDesignacion | undefined;
  error?: string;
  onConfirmar: () => void;
}

export function ModalDesactivarPeriodo({
  open,
  onOpenChange,
  periodo,
  error,
  onConfirmar,
}: ModalDesactivarPeriodoProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Desactivar período"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          <Button variant="destructive" onClick={onConfirmar}>
            Desactivar
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo desactivar">
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          ¿Estás seguro de que querés desactivar el período <strong>"{periodo?.nombre}"</strong>?
        </p>
        <p
          style={{
            margin: 0,
            fontSize: "var(--text-body-sm-size)",
            color: "var(--color-text-secondary)",
          }}
        >
          El Jefe de Cátedra ya no va a poder cargar pedidos de designación para este período.
        </p>
      </div>
    </Modal>
  );
}
