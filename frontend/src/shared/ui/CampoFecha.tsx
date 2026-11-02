import { useRef, type ChangeEvent } from "react";
import { fechaISOATexto, fechaTextoAISO, formatearFecha } from "./camposFechaHora";
import { aplicarMascara } from "./mascara";
import "./formulario.css";

interface CampoFechaProps {
  id?: string;
  /** Texto "dd/mm/aaaa" tal como lo escribió la persona. */
  value: string;
  onChange: (texto: string) => void;
  disabled?: boolean;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  "aria-label"?: string;
}

/**
 * Fecha enmascarada dd/mm/aaaa. No usa el `type="date"` visible porque el navegador lo muestra
 * mm/dd/yyyy según su idioma; el calendario es un botón que abre un input date nativo oculto.
 * Pensado para usarse dentro de `Field`, que le inyecta id y aria-*.
 */
export function CampoFecha({
  id,
  value,
  onChange,
  disabled,
  "aria-invalid": ariaInvalid,
  "aria-describedby": ariaDescribedby,
  "aria-label": ariaLabel,
}: CampoFechaProps) {
  const texto = useRef<HTMLInputElement>(null);
  const nativo = useRef<HTMLInputElement>(null);

  function abrirCalendario() {
    const campo = nativo.current;
    if (!campo) return;
    campo.value = fechaTextoAISO(value) ?? "";
    try {
      campo.showPicker();
    } catch {
      // Sin showPicker (o sin gesto de usuario): el foco vuelve al campo de texto.
      texto.current?.focus();
    }
  }

  return (
    <span className="adoc-fecha">
      <input
        ref={texto}
        id={id}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        placeholder="dd/mm/aaaa"
        maxLength={10}
        disabled={disabled}
        value={value}
        className="adoc-input"
        aria-invalid={ariaInvalid}
        aria-describedby={ariaDescribedby}
        aria-label={ariaLabel}
        onChange={(evento) => aplicarMascara(evento, formatearFecha, onChange)}
      />
      <button
        type="button"
        className="adoc-fecha-btn"
        aria-label="Abrir calendario"
        disabled={disabled}
        onClick={abrirCalendario}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <rect x="3" y="4" width="18" height="18" rx="2" />
          <path d="M16 2v4M8 2v4M3 10h18" />
        </svg>
      </button>
      <input
        ref={nativo}
        id={id ? `${id}-calendario` : undefined}
        type="date"
        tabIndex={-1}
        aria-hidden="true"
        data-no-foco="true"
        className="adoc-fecha-nativo"
        onChange={(evento: ChangeEvent<HTMLInputElement>) => {
          if (evento.target.value) onChange(fechaISOATexto(evento.target.value));
        }}
      />
    </span>
  );
}
