import { Modal, Button } from "@ars-docendi/ui";

interface ModalConfirmarEliminarProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Título del diálogo, p. ej. "Eliminar certificación". */
  titulo: string;
  /** Qué se está por borrar, para que el docente lo reconozca. */
  nombre: string;
  onConfirmar: () => void;
}

/**
 * Confirmación de borrado. Sigue el patrón de ModalEliminarPeriodo: qué se
 * borra + aviso de que no se puede deshacer. Sin justificativo: es información
 * propia del docente.
 */
export function ModalConfirmarEliminar({
  open,
  onOpenChange,
  titulo,
  nombre,
  onConfirmar,
}: ModalConfirmarEliminarProps) {
  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={titulo}
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
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>
          ¿Estás seguro de que querés eliminar <strong>"{nombre}"</strong>?
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
