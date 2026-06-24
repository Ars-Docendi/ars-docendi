import { Modal, Button, InlineAlert } from "@ars-docendi/ui";
import type { PeriodoDesignacion } from "../types";

interface ModalEliminarPeriodoProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  periodo: PeriodoDesignacion | undefined;
  error?: string;
  onConfirmar: () => void;
}

export function ModalEliminarPeriodo({
  open,
  onOpenChange,
  periodo,
  error,
  onConfirmar,
}: ModalEliminarPeriodoProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Eliminar período"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          <Button variant="destructive" onClick={onConfirmar}>
            Eliminar
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se puede eliminar">
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          ¿Estás seguro de que querés eliminar el período <strong>"{periodo?.nombre}"</strong>?
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
