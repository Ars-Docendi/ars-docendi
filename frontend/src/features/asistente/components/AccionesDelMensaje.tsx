import { useEffect, useRef, useState } from "react";
import { Button } from "@ars-docendi/ui";

import { copyIcon } from "../../../app/shell/icons";
import { copiar, hayPortapapeles, tablaComoTsv } from "../utils/portapapeles";
import type { ColumnaDelResultado } from "../types";

/** Cuánto dura «Copiado» antes de volver a la etiqueta de siempre. */
export const DURACION_DEL_COPIADO_MS = 2000;

interface AccionesDelMensajeProps {
  /** El texto de la respuesta, tal cual llegó. */
  texto: string;
  /** La tabla de resultados, si la hay. */
  tabla?: { columnas: ColumnaDelResultado[]; filas: unknown[][] };
  /** Para el test, que no puede esperar el tiempo real. */
  duracionDelCopiadoMs?: number;
}

/**
 * Lo que se puede hacer con un mensaje, y hoy es una sola cosa: copiarlo.
 * Regenerar, calificar, editar y adjuntar no tienen backend, y un botón que no
 * hace nada es el fake UI que el invariante #7 prohíbe.
 *
 * SIEMPRE VISIBLES, no sólo al pasar el puntero: el hover no existe con el
 * teclado ni con el dedo. Y SÓLO SI HAY PORTAPAPELES: en un contexto sin él
 * —http sin TLS, un navegador viejo— no se renderiza nada, porque un botón que
 * falla al pulsarlo tampoco es real.
 */
export function AccionesDelMensaje({
  texto,
  tabla,
  duracionDelCopiadoMs = DURACION_DEL_COPIADO_MS,
}: AccionesDelMensajeProps) {
  if (!hayPortapapeles()) return null;

  return (
    <div className="adoc-asistente-acciones">
      <BotonDeCopiar
        etiqueta="Copiar respuesta"
        contenido={() => texto}
        duracionMs={duracionDelCopiadoMs}
      />

      {tabla && (
        <BotonDeCopiar
          etiqueta="Copiar tabla"
          contenido={() => tablaComoTsv(tabla.columnas, tabla.filas)}
          duracionMs={duracionDelCopiadoMs}
        />
      )}
    </div>
  );
}

interface BotonDeCopiarProps {
  etiqueta: string;
  /** Se arma recién al pulsar: la tabla como texto no se necesita antes. */
  contenido: () => string;
  duracionMs: number;
}

/** Un botón que confirma cambiando su etiqueta a «Copiado» durante un momento. */
function BotonDeCopiar({ etiqueta, contenido, duracionMs }: BotonDeCopiarProps) {
  const [copiado, setCopiado] = useState(false);
  const temporizador = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(temporizador.current), []);

  async function alPulsar() {
    try {
      await copiar(contenido());
    } catch {
      // El navegador lo negó —permiso, o el documento perdió el foco—. El texto
      // sigue ahí para seleccionarlo a mano; la etiqueta no miente diciendo
      // «Copiado».
      return;
    }

    setCopiado(true);
    window.clearTimeout(temporizador.current);
    temporizador.current = window.setTimeout(() => setCopiado(false), duracionMs);
  }

  return (
    <Button variant="ghost" size="sm" leadingIcon={copyIcon} onClick={() => void alPulsar()}>
      {copiado ? "Copiado" : etiqueta}
    </Button>
  );
}
