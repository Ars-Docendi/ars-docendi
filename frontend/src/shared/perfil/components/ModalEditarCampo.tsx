import { useState } from "react";
import { Button, Field, Input } from "@ars-docendi/ui";
import { Modal } from "../../ui/Modal";

interface ModalEditarCampoProps {
  /** Nombre del campo, p. ej. "Teléfono". */
  etiqueta: string;
  valor: string;
  tipo?: "text" | "email" | "tel";
  /** Devuelve el mensaje de error, o undefined si el valor es válido. */
  validar?: (valor: string) => string | undefined;
  onCerrar: () => void;
  onGuardar: (valor: string) => void;
}

/** Edición de un único campo del perfil, abierta desde su fila. */
export function ModalEditarCampo({
  etiqueta,
  valor,
  tipo = "text",
  validar,
  onCerrar,
  onGuardar,
}: ModalEditarCampoProps) {
  const [borrador, setBorrador] = useState(valor);
  const [error, setError] = useState<string | undefined>();
  // Lo cargado se compara con lo que se abrió: cerrar con datos sin guardar pide confirmar.
  const [inicial] = useState(() => JSON.stringify(borrador));

  function guardar() {
    const limpio = borrador.trim();
    const mensaje = validar?.(limpio);
    if (mensaje) {
      setError(mensaje);
      return;
    }
    onGuardar(limpio);
  }

  return (
    <Modal
      open
      sucio={JSON.stringify(borrador) !== inicial}
      onOpenChange={(abierto) => !abierto && onCerrar()}
      title={etiqueta}
      footer={
        <>
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={guardar}>
            Guardar
          </Button>
        </>
      }
    >
      <Field label={etiqueta} error={error}>
        <Input
          type={tipo}
          value={borrador}
          autoFocus
          onChange={(e) => setBorrador(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              guardar();
            }
          }}
        />
      </Field>
    </Modal>
  );
}
