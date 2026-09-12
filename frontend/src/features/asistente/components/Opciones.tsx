import { Button } from "@ars-docendi/ui";

import type { OpcionDeAclaracion } from "../types";

interface OpcionesProps {
  opciones: OpcionDeAclaracion[];
  onElegir: (pregunta: string) => void;
  deshabilitado: boolean;
}

/**
 * El menú de una aclaración.
 *
 * BLOQUEAN el turno: hasta que el usuario elija, la pregunta original no se puede
 * responder. Por eso se presentan como una elección que continúa lo que empezó —y no
 * como preguntas nuevas, que es lo que son las sugerencias—.
 */
export function Opciones({ opciones, onElegir, deshabilitado }: OpcionesProps) {
  if (opciones.length === 0) return null;

  return (
    <div className="adoc-asistente-opciones">
      <p className="adoc-asistente-opciones-titulo">Elegí una para continuar:</p>
      <ul>
        {opciones.map((opcion) => (
          <li key={opcion.etiqueta}>
            <Button
              variant="secondary"
              size="sm"
              disabled={deshabilitado}
              onClick={() => onElegir(opcion.etiqueta)}
            >
              {opcion.etiqueta}
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
