import { formatearHora } from "./camposFechaHora";
import { aplicarMascara } from "./mascara";

interface CampoHoraProps {
  id?: string;
  /** Texto "hh:mm" tal como lo escribió la persona. */
  value: string;
  onChange: (texto: string) => void;
  disabled?: boolean;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
}

/** Horario enmascarado hh:mm de 24 h (el navegador mostraría AM/PM según su idioma). */
export function CampoHora({
  id,
  value,
  onChange,
  disabled,
  "aria-invalid": ariaInvalid,
  "aria-describedby": ariaDescribedby,
}: CampoHoraProps) {
  return (
    <input
      id={id}
      type="text"
      inputMode="numeric"
      autoComplete="off"
      placeholder="hh:mm"
      maxLength={5}
      disabled={disabled}
      value={value}
      className="adoc-input"
      aria-invalid={ariaInvalid}
      aria-describedby={ariaDescribedby}
      onChange={(evento) => aplicarMascara(evento, formatearHora, onChange)}
    />
  );
}
