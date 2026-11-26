import { useState } from "react";
import { Button, Field, InlineAlert, Textarea } from "@ars-docendi/ui";
import { Modal } from "../../../shared/ui/Modal";
import type { PedidoConciliacion } from "../api/conciliacionApi";
import { NovedadChip } from "./NovedadChip";

interface ModalRechazarConciliacionProps {
  pedido: PedidoConciliacion;
  docente: string;
  rechazando: boolean;
  error?: string;
  onCancelar: () => void;
  onConfirmar: (motivo: string) => void;
}

/** Rechazo de un pedido en la conciliación: el motivo es obligatorio. */
export function ModalRechazarConciliacion({
  pedido,
  docente,
  rechazando,
  error,
  onCancelar,
  onConfirmar,
}: ModalRechazarConciliacionProps) {
  const [motivo, setMotivo] = useState("");
  const [intentado, setIntentado] = useState(false);
  const falta = motivo.trim() === "";

  function confirmar() {
    setIntentado(true);
    if (!falta) onConfirmar(motivo.trim());
  }

  return (
    <Modal
      open
      tamano="confirmacion"
      title="Rechazar cambio"
      sucio={motivo.trim() !== ""}
      pendiente={rechazando}
      onOpenChange={(abierto) => !abierto && onCancelar()}
      alEnviar={confirmar}
      footer={
        <>
          <Button variant="secondary" onClick={onCancelar} disabled={rechazando}>
            Cancelar
          </Button>
          <Button type="submit" variant="destructive" loading={rechazando}>
            Rechazar
          </Button>
        </>
      }
    >
      <div className="conc-modal-cuerpo">
        {error && (
          <InlineAlert severity="danger" title="No se pudo rechazar">
            {error}
          </InlineAlert>
        )}
        <p>
          <NovedadChip novedad={pedido.novedad} /> <strong>{pedido.materia}</strong> de {docente} (
          {pedido.numero}).
        </p>
        <p>
          El cambio no se aplica, sale del Excel del lote y queda en Rechazados. Después podés
          editarlo y volver a conciliarlo.
        </p>
        <Field
          label="Motivo"
          required
          error={intentado && falta ? "Escribí el motivo del rechazo." : undefined}
        >
          <Textarea
            rows={3}
            value={motivo}
            invalid={intentado && falta}
            placeholder="Por ejemplo: la rectificación no aprobó el ascenso"
            onChange={(evento) => setMotivo(evento.target.value)}
          />
        </Field>
      </div>
    </Modal>
  );
}
