import { Modal, Button, InlineAlert } from "@ars-docendi/ui";
import type { PedidoDesignacion } from "../types";

interface ModalEliminarPedidoProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pedido: PedidoDesignacion | undefined;
  error?: string;
  eliminando?: boolean;
  onConfirmar: () => void;
}

/** Confirmación para eliminar un pedido en borrador — mismo patrón que `ModalEliminarPeriodo`. */
export function ModalEliminarPedido({
  open,
  onOpenChange,
  pedido,
  error,
  eliminando = false,
  onConfirmar,
}: ModalEliminarPedidoProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title="Eliminar pedido"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={eliminando}>
            Cancelar
          </Button>
          <Button variant="destructive" onClick={onConfirmar} loading={eliminando}>
            Eliminar
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo eliminar">
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          ¿Estás seguro de que querés eliminar el pedido de{" "}
          <strong>"{pedido?.docente.nombre}"</strong>?
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
