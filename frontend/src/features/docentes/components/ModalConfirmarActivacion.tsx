import { Button, Modal } from "@ars-docendi/ui";
import { nombreCompleto, type DocenteMock } from "../mock/mockStore";

interface ModalConfirmarActivacionProps {
  docente: DocenteMock | null;
  onConfirmar: () => void;
  onCerrar: () => void;
}

export function ModalConfirmarActivacion({
  docente,
  onConfirmar,
  onCerrar,
}: ModalConfirmarActivacionProps) {
  return (
    <Modal
      open={docente !== null}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Activar docente"
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
        ¿Confirmás que querés activar a <strong>{docente ? nombreCompleto(docente) : ""}</strong>?
      </p>
    </Modal>
  );
}
