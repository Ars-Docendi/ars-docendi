import { Button, Modal } from "@ars-docendi/ui";
import { nombreCompleto, type DocenteMock } from "../mock/mockStore";

interface ModalConfirmarDesactivacionProps {
  docente: DocenteMock | null;
  onConfirmar: () => void;
  onCerrar: () => void;
}

export function ModalConfirmarDesactivacion({
  docente,
  onConfirmar,
  onCerrar,
}: ModalConfirmarDesactivacionProps) {
  return (
    <Modal
      open={docente !== null}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Desactivar docente"
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
        ¿Confirmás que querés desactivar a <strong>{docente ? nombreCompleto(docente) : ""}</strong>
        ? El docente dejará de aparecer como activo en el sistema.
      </p>
    </Modal>
  );
}
