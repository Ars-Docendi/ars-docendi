import { Button, Modal } from "@ars-docendi/ui";
import { nombreCompleto, type DocenteMock } from "../models";

interface ModalConfirmarEstadoProps {
  docente: DocenteMock | null;
  activar?: boolean;
  onConfirmar: () => void;
  onCerrar: () => void;
}

export function ModalConfirmarEstado({
  docente,
  activar = false,
  onConfirmar,
  onCerrar,
}: ModalConfirmarEstadoProps) {
  const accion = activar ? "Activar" : "Desactivar";
  return (
    <Modal
      open={docente !== null}
      onOpenChange={(next) => !next && onCerrar()}
      title={`${accion} docente`}
      footer={
        <div
          className="adoc-modal-actions"
          style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: "1rem" }}
        >
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button
            variant={activar ? "primary" : "secondary"}
            style={
              activar
                ? undefined
                : {
                    background: "var(--danger-500)",
                    color: "#fff",
                    borderColor: "var(--danger-500)",
                  }
            }
            onClick={onConfirmar}
          >
            {accion}
          </Button>
        </div>
      }
    >
      <p>
        ¿Confirmás que querés {accion.toLowerCase()} a{" "}
        <strong>{docente ? nombreCompleto(docente) : ""}</strong>?
        {!activar && " El docente dejará de aparecer como activo en el sistema."}
      </p>
    </Modal>
  );
}
