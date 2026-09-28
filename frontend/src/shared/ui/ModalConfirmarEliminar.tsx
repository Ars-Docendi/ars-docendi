import type { ReactNode } from "react";
import { Button, InlineAlert, Modal } from "@ars-docendi/ui";

interface ModalConfirmarEliminarProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Título del diálogo, p. ej. "Eliminar período". */
  titulo: string;
  /** Qué se borra, para que se reconozca: p. ej. `el período <strong>"X"</strong>`. */
  objeto: ReactNode;
  /** Motivo por el que el borrado falló; el diálogo queda abierto. */
  error?: string;
  /** Borrado en curso: bloquea Cancelar y muestra la carga en Eliminar. */
  eliminando?: boolean;
  onConfirmar: () => void;
}

/** Confirmación de borrado única para todas las pantallas. */
export function ModalConfirmarEliminar({
  open,
  onOpenChange,
  titulo,
  objeto,
  error,
  eliminando = false,
  onConfirmar,
}: ModalConfirmarEliminarProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={titulo}
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
          ¿Estás seguro de que querés eliminar {objeto}?
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
