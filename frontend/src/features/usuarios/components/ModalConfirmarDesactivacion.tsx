import { Button, Modal } from "@ars-docendi/ui";
import { nombreCompleto, type UsuarioMock } from "../models";

interface ModalConfirmarDesactivacionProps {
  usuario: UsuarioMock | null;
  onConfirmar: () => void;
  onCerrar: () => void;
}

export function ModalConfirmarDesactivacion({
  usuario,
  onConfirmar,
  onCerrar,
}: ModalConfirmarDesactivacionProps) {
  return (
    <Modal
      open={usuario !== null}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Desactivar usuario"
      footer={
        <div
          className="adoc-modal-actions"
          style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: "1rem" }}
        >
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button
            variant="secondary"
            style={{
              background: "var(--danger-500)",
              color: "#fff",
              borderColor: "var(--danger-500)",
            }}
            onClick={onConfirmar}
          >
            Desactivar
          </Button>
        </div>
      }
    >
      <p>
        ¿Confirmás que querés desactivar a <strong>{usuario ? nombreCompleto(usuario) : ""}</strong>
        ? El usuario dejará de poder acceder al sistema.
      </p>
    </Modal>
  );
}
