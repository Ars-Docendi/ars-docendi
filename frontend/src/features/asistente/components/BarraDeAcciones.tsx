import { useEffect, useRef, useState, type RefObject } from "react";

import { VotoDeRetroalimentacion } from "./VotoDeRetroalimentacion";
import { ampliarTablaIcon, checkIcon, copyIcon, downloadIcon } from "../../../app/shell/icons";
import { copiar, hayPortapapeles } from "../utils/portapapeles";

/** Cuánto dura el ícono de tilde en «Copiar respuesta» antes de volver al de copiar. */
export const DURACION_DEL_COPIADO_MS = 2000;

interface BarraDeAccionesProps {
  /** El texto de la respuesta, tal cual llegó. */
  texto: string;
  /**
   * Exportar, YA resuelto contra el orden mostrado —la misma función que
   * `TablaDeResultado` expone vía `onExportarDisponible`—, o `null` mientras
   * no hay tabla que exportar. `null` es también la señal de que no hay
   * «Ampliar tabla» ni «Exportar a CSV» para este turno
   * (asistente-superficie-frontend: sólo con al menos una fila).
   */
  onExportar: (() => void) | null;
  /** Abre la vista ampliada — el estado y el cierre los maneja `TablaDeResultado` (controlada). */
  onAmpliar: () => void;
  /** A dónde vuelve el foco cuando la vista ampliada se contrae desde adentro. */
  ampliarBotonRef: RefObject<HTMLButtonElement | null>;
  /** Absent on any turn that is not an answered one: no vote buttons then. */
  claveDeRetroalimentacion?: string | null;
  /** Para el test, que no puede esperar el tiempo real. */
  duracionDelCopiadoMs?: number;
}

/**
 * La barra de íconos bajo cada respuesta (asistente-superficie-frontend,
 * design.md D6 de asistente-rediseno-v3): reemplaza los botones de texto de
 * `AccionesDelMensaje` (borrado) y de la propia tabla —«Ampliar tabla» /
 * «Exportar a CSV», que la tabla deja de dibujar cuando recibe sus
 * disparadores controlados (`ampliado`/`onAmpliarChange`/
 * `onExportarDisponible`)— y los botones de texto de `VotoDeRetroalimentacion`.
 *
 * SIEMPRE EN EL DOM Y EN EL ORDEN DE TAB: la visibilidad la decide
 * `asistente.css` con opacidad —hover o foco dentro del turno, siempre en el
 * último turno y en los votados (`:has([aria-pressed="true"])`, sin estado
 * nuevo que levantar)—, nunca `display: none` ni un render condicional por
 * interacción. Lo que SÍ decide este componente es qué controles existen: sin
 * portapapeles no hay «Copiar respuesta», sin tabla no hay «Ampliar tabla» ni
 * «Exportar a CSV», sin token no hay voto — y sin ninguno de los tres, la
 * barra entera no se monta.
 */
export function BarraDeAcciones({
  texto,
  onExportar,
  onAmpliar,
  ampliarBotonRef,
  claveDeRetroalimentacion,
  duracionDelCopiadoMs = DURACION_DEL_COPIADO_MS,
}: BarraDeAccionesProps) {
  const hayCopiar = hayPortapapeles();
  const hayTabla = onExportar !== null;
  const hayVoto = Boolean(claveDeRetroalimentacion);

  if (!hayCopiar && !hayTabla && !hayVoto) return null;

  return (
    <div className="adoc-asistente-barra">
      {hayCopiar && <BotonDeCopiar texto={texto} duracionMs={duracionDelCopiadoMs} />}

      {hayTabla && (
        <>
          <button
            ref={ampliarBotonRef}
            type="button"
            className="adoc-asistente-barra-boton"
            title="Ampliar tabla"
            aria-label="Ampliar tabla"
            onClick={onAmpliar}
          >
            {ampliarTablaIcon}
          </button>
          <button
            type="button"
            className="adoc-asistente-barra-boton"
            title="Exportar a CSV"
            aria-label="Exportar a CSV"
            onClick={() => onExportar?.()}
          >
            {downloadIcon}
          </button>
        </>
      )}

      {(hayCopiar || hayTabla) && hayVoto && (
        <span className="adoc-asistente-barra-separador" aria-hidden="true" />
      )}

      <VotoDeRetroalimentacion claveDeRetroalimentacion={claveDeRetroalimentacion} />
    </div>
  );
}

function BotonDeCopiar({ texto, duracionMs }: { texto: string; duracionMs: number }) {
  const [copiado, setCopiado] = useState(false);
  const temporizador = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(temporizador.current), []);

  async function alPulsar() {
    try {
      await copiar(texto);
    } catch {
      // El navegador lo negó —permiso, o el documento perdió el foco—. El texto
      // sigue ahí para seleccionarlo a mano; el ícono no miente mostrando un
      // tilde que no pasó.
      return;
    }

    setCopiado(true);
    window.clearTimeout(temporizador.current);
    temporizador.current = window.setTimeout(() => setCopiado(false), duracionMs);
  }

  return (
    <button
      type="button"
      className="adoc-asistente-barra-boton"
      title={copiado ? "Copiado" : "Copiar respuesta"}
      aria-label={copiado ? "Copiado" : "Copiar respuesta"}
      onClick={() => void alPulsar()}
    >
      <span aria-hidden="true">{copiado ? checkIcon : copyIcon}</span>
    </button>
  );
}
