import { Button, Modal } from "@ars-docendi/ui";
import type { RolMock } from "../models";

interface ModalConfirmarEliminarRolProps {
  rol: RolMock | null;
  onConfirmar: () => void;
  onCerrar: () => void;
}

export function ModalConfirmarEliminarRol({
  rol,
  onConfirmar,
  onCerrar,
}: ModalConfirmarEliminarRolProps) {
  return (
    <Modal
      open={!!rol}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Eliminar rol"
      footer={
        <div
          style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: "1rem" }}
        >
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button
            variant="primary"
            style={{
              background: "var(--danger-500)",
              borderColor: "var(--danger-500)",
              color: "#fff",
            }}
            onClick={onConfirmar}
          >
            Eliminar
          </Button>
        </div>
      }
    >
      <p style={{ margin: 0 }}>
        ¿Estás seguro de que querés eliminar el rol <strong>{rol?.nombre}</strong>? También se
        eliminarán sus permisos asignados. Esta acción no se puede deshacer.
      </p>
    </Modal>
  );
}
