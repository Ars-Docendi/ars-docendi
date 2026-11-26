interface InterruptorProps {
  id: string;
  /** Id of the element that names the switch. */
  etiquetadoPor: string;
  /** Id of the element that explains it, if any. */
  descritoPor?: string;
  activo: boolean;
  disabled?: boolean;
  onCambiar: (activo: boolean) => void;
}

/** Accessible on/off switch: a native button with `role="switch"` (Space and Enter toggle it). */
export function Interruptor({
  id,
  etiquetadoPor,
  descritoPor,
  activo,
  disabled = false,
  onCambiar,
}: InterruptorProps) {
  return (
    <button
      type="button"
      role="switch"
      id={id}
      className="adoc-notif-interruptor"
      aria-checked={activo}
      aria-labelledby={etiquetadoPor}
      aria-describedby={descritoPor}
      disabled={disabled}
      onClick={() => onCambiar(!activo)}
    >
      <span className="adoc-notif-interruptor-perilla" aria-hidden="true" />
    </button>
  );
}
