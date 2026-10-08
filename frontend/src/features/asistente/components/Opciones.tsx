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
 * responder. Es el único campo del turno que ofrece algo para elegir: desde
 * ARS-149 el asistente ya no sugiere próximos pasos después de un rechazo o de
 * una respuesta — los únicos ejemplos clicables son los de la bienvenida
 * (`EstadoInicial`), que no bloquean nada porque ahí todavía no hay turno.
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
