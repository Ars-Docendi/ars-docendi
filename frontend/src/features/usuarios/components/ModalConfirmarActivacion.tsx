import { Button, Modal } from "@ars-docendi/ui";
import { nombreCompleto, type UsuarioMock } from "../models";

interface ModalConfirmarActivacionProps {
  usuario: UsuarioMock | null;
  onConfirmar: () => void;
  onCerrar: () => void;
}

export function ModalConfirmarActivacion({
  usuario,
  onConfirmar,
  onCerrar,
}: ModalConfirmarActivacionProps) {
  return (
    <Modal
      open={usuario !== null}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Activar usuario"
      footer={
        <div
          className="adoc-modal-actions"
          style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: "1rem" }}
        >
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={onConfirmar}>
            Activar
          </Button>
        </div>
      }
    >
      <p>
        ¿Confirmás que querés activar a <strong>{usuario ? nombreCompleto(usuario) : ""}</strong>?
        El usuario recuperará acceso al sistema.
      </p>
    </Modal>
  );
}
