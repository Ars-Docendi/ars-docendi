import { useState } from "react";

import { CampoFecha } from "./CampoFecha";
import { fechaISOATexto, fechaTextoAISO } from "./camposFechaHora";

interface CampoFechaISOProps {
  id?: string;
  /** Fecha ISO `aaaa-mm-dd`, o cadena vacía. */
  value: string;
  /** Recibe la fecha ISO en cuanto el texto es una fecha real; mientras se escribe, cadena vacía. */
  onChange: (iso: string) => void;
  disabled?: boolean;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  "aria-label"?: string;
}

/**
 * `CampoFecha` para formularios y filtros cuyo valor es una fecha ISO (el DTO y la URL no cambian).
 * Guarda el texto que la persona escribe y sólo informa la fecha cuando está completa y es real.
 */
export function CampoFechaISO({ value, onChange, ...resto }: CampoFechaISOProps) {
  const [texto, setTexto] = useState(() => fechaISOATexto(value));
  // Valor ISO que el campo ya conoce: si el padre lo cambia por otro lado (reset, otra fila),
  // el texto se reescribe; si es el eco de lo que se acaba de escribir, no.
  const [conocido, setConocido] = useState(value);
  if (value !== conocido) {
    setConocido(value);
    if ((fechaTextoAISO(texto) ?? "") !== value) setTexto(fechaISOATexto(value));
  }

  return (
    <CampoFecha
      {...resto}
      value={texto}
      onChange={(nuevo) => {
        const iso = fechaTextoAISO(nuevo) ?? "";
        setTexto(nuevo);
        setConocido(iso);
        onChange(iso);
      }}
    />
  );
}
