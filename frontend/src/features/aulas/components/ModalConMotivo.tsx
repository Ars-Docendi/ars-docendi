import { useState, type ReactNode } from "react";
import { Button, Field, InlineAlert, Textarea } from "@ars-docendi/ui";
import { ModalDeFormulario } from "../../../shared/ui/ModalDeFormulario";

interface ModalConMotivoProps {
  titulo: string;
  /** Prefijo de los ids de los controles: `<prefijo>-motivo`, `-volver`, `-confirmar`. */
  prefijo: string;
  etiquetaConfirmar: string;
  tituloError: string;
  /** Texto bajo el campo cuando se intenta confirmar sin motivo. */
  errorMotivoVacio: string;
  placeholder: string;
  /** Texto que identifica la solicitud. */
  descripcion: ReactNode;
  /** Aviso informativo opcional entre la descripción y el motivo. */
  aviso?: { titulo: string; texto: string };
  error?: string;
  pendiente?: boolean;
  onCerrar: () => void;
  onConfirmar: (motivo: string) => void;
}

/** Confirmación destructiva con motivo obligatorio (rechazar una solicitud, cancelar una reserva). */
export function ModalConMotivo({
  titulo,
  prefijo,
  etiquetaConfirmar,
  tituloError,
  errorMotivoVacio,
  placeholder,
  descripcion,
  aviso,
  error,
  pendiente = false,
  onCerrar,
  onConfirmar,
}: ModalConMotivoProps) {
  const [motivo, setMotivo] = useState("");
  const [mostrarError, setMostrarError] = useState(false);
  const idMotivo = `${prefijo}-motivo`;

  function confirmar() {
    if (pendiente) return;
    if (!motivo.trim()) {
      setMostrarError(true);
      document.getElementById(idMotivo)?.focus();
      return;
    }
    onConfirmar(motivo.trim());
  }

  return (
    <ModalDeFormulario
      titulo={titulo}
      onCerrar={onCerrar}
      pendiente={pendiente}
      sucio={motivo.trim() !== ""}
      pie={
        <>
          <Button
            id={`${prefijo}-volver`}
            variant="secondary"
            onClick={onCerrar}
            disabled={pendiente}
          >
            Volver
          </Button>
          <Button
            id={`${prefijo}-confirmar`}
            variant="destructive"
            onClick={confirmar}
            loading={pendiente}
          >
            {etiquetaConfirmar}
          </Button>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {error && (
          <InlineAlert severity="danger" title={tituloError}>
            {error}
          </InlineAlert>
        )}
        <p style={{ margin: 0, color: "var(--color-text-primary)" }}>{descripcion}</p>
        {aviso && (
          <InlineAlert severity="info" title={aviso.titulo}>
            {aviso.texto}
          </InlineAlert>
        )}
        <Field
          label="Motivo"
          required
          error={mostrarError && !motivo.trim() ? errorMotivoVacio : undefined}
        >
          <Textarea
            id={idMotivo}
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder={placeholder}
            rows={3}
          />
        </Field>
      </div>
    </ModalDeFormulario>
  );
}
