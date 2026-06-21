import { useState } from "react";
import { Button, Modal, Textarea } from "@ars-docendi/ui";
import type { ButtonVariant } from "@ars-docendi/ui";
import type { PedidoDesignacion } from "../types";

export type AccionRevision = "aceptar" | "rechazar" | "devolver" | "priorizar";

interface ConfigAccion {
  titulo: string;
  etiqueta: string;
  variante: ButtonVariant;
  requiereComentario: boolean;
  descripcion: string;
  placeholder: string;
}

const CONFIG: Record<AccionRevision, ConfigAccion> = {
  aceptar: {
    titulo: "Aceptar pedido",
    etiqueta: "Aceptar",
    variante: "primary",
    requiereComentario: false,
    descripcion: "Vas a aceptar y avanzar a la etapa siguiente el pedido de",
    placeholder: "Comentario (opcional)",
  },
  rechazar: {
    titulo: "Rechazar pedido",
    etiqueta: "Rechazar",
    variante: "destructive",
    requiereComentario: true,
    descripcion: "Vas a rechazar (de forma definitiva) el pedido de",
    placeholder: "Justificativo del rechazo (obligatorio)",
  },
  devolver: {
    titulo: "Devolver pedido",
    etiqueta: "Devolver",
    variante: "warning",
    requiereComentario: true,
    descripcion: "Vas a devolver para corrección el pedido de",
    placeholder: "Comentario para la corrección (obligatorio)",
  },
  priorizar: {
    titulo: "Marcar prioritario",
    etiqueta: "Marcar prioritario",
    variante: "primary",
    requiereComentario: true,
    descripcion: "Vas a marcar como prioritario (sin cambiar el estado) el pedido de",
    placeholder: "Motivo de la prioridad (obligatorio)",
  },
};

interface ModalAccionRevisionProps {
  /** Acción a confirmar; `null` cierra el modal. */
  accion: AccionRevision | null;
  pedido: PedidoDesignacion;
  enviando?: boolean;
  onCerrar: () => void;
  onConfirmar: (comentario: string) => void;
}

/**
 * Modal de confirmación de una acción de revisión. Aplica la regla de
 * comentario [BR-005]: obligatorio en rechazar/devolver/priorizar, opcional en
 * aceptar. El dominio sigue siendo la autoridad; esto adelanta el feedback.
 *
 * El formulario interno se remonta por `key={accion}`, así el estado (comentario
 * tocado) se resetea al cambiar de acción sin un effect de reset.
 */
export function ModalAccionRevision({ accion, ...resto }: ModalAccionRevisionProps) {
  if (!accion) {
    return null;
  }
  return <FormularioAccion key={accion} accion={accion} {...resto} />;
}

interface FormularioAccionProps extends Omit<ModalAccionRevisionProps, "accion"> {
  accion: AccionRevision;
}

function FormularioAccion({
  accion,
  pedido,
  enviando = false,
  onCerrar,
  onConfirmar,
}: FormularioAccionProps) {
  const [comentario, setComentario] = useState("");
  const [tocado, setTocado] = useState(false);

  const config = CONFIG[accion];
  const faltaComentario = config.requiereComentario && comentario.trim() === "";

  function handleConfirmar() {
    if (faltaComentario) {
      setTocado(true);
      return;
    }
    onConfirmar(comentario.trim());
  }

  return (
    <Modal
      open
      onOpenChange={(abierto) => {
        if (!abierto) onCerrar();
      }}
      title={config.titulo}
      footer={
        <>
          <Button variant="secondary" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Button>
          <Button
            variant={config.variante}
            onClick={handleConfirmar}
            loading={enviando}
            disabled={enviando}
          >
            {config.etiqueta}
          </Button>
        </>
      }
    >
      <p style={{ marginTop: 0 }}>
        {config.descripcion} <strong>{pedido.docente.nombre}</strong>.
      </p>
      <label
        htmlFor="comentario-accion-revision"
        style={{
          display: "block",
          marginBottom: "var(--space-1)",
          fontSize: "var(--text-body-sm-size)",
          color: "var(--color-text-secondary)",
        }}
      >
        {config.requiereComentario ? "Comentario (obligatorio)" : "Comentario (opcional)"}
      </label>
      <Textarea
        id="comentario-accion-revision"
        value={comentario}
        onChange={(e) => setComentario(e.target.value)}
        placeholder={config.placeholder}
        invalid={tocado && faltaComentario}
        rows={4}
      />
      {tocado && faltaComentario && (
        <p
          role="alert"
          style={{
            color: "var(--color-text-danger)",
            fontSize: "var(--text-body-sm-size)",
            marginBottom: 0,
          }}
        >
          El comentario es obligatorio para {config.etiqueta.toLowerCase()}.
        </p>
      )}
    </Modal>
  );
}
