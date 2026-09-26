import { useEffect, useRef, useState, type Ref } from "react";

import { DURACION_DEL_COPIADO_MS } from "./BarraDeAcciones";
import { checkIcon, copyIcon, pencilIcon } from "../../../app/shell/icons";
import { copiar, hayPortapapeles } from "../utils/portapapeles";

interface HerramientasDePreguntaProps {
  /** El texto de la pregunta, tal cual se envió. */
  pregunta: string;
  /**
   * Abre la edición de esta pregunta. Ausente en cualquier pregunta que no
   * sea la última, o mientras un turno está en vuelo o el composer está
   * bloqueado (asistente-edicion-de-la-ultima-pregunta): sin esta prop, el
   * control «Editar y reenviar» no se monta.
   */
  onEditar?: () => void;
  /** Al botón «Editar y reenviar», para devolverle el foco al cancelar la edición. */
  editarBotonRef?: Ref<HTMLButtonElement>;
  /** Para el test, que no puede esperar el tiempo real. */
  duracionDelCopiadoMs?: number;
}

/**
 * Las herramientas de una pregunta: «Copiar pregunta» y, sólo en la última,
 * «Editar y reenviar» (asistente-edicion-de-la-ultima-pregunta, design.md D9
 * de asistente-rediseno-v3).
 *
 * SE REVELAN CON EL HOVER O EL FOCO DEL TURNO, igual que
 * <see cref="BarraDeAcciones" />: `asistente.css` decide la opacidad con el
 * mismo mecanismo — nunca `display`/`visibility`, para que Tab las alcance
 * igual con la fila "apagada".
 */
export function HerramientasDePregunta({
  pregunta,
  onEditar,
  editarBotonRef,
  duracionDelCopiadoMs = DURACION_DEL_COPIADO_MS,
}: HerramientasDePreguntaProps) {
  const hayCopiar = hayPortapapeles();

  if (!hayCopiar && !onEditar) return null;

  return (
    <div className="adoc-asistente-pregunta-herramientas">
      {hayCopiar && <BotonDeCopiarPregunta texto={pregunta} duracionMs={duracionDelCopiadoMs} />}

      {onEditar && (
        <button
          ref={editarBotonRef}
          type="button"
          className="adoc-asistente-barra-boton"
          title="Editar y reenviar"
          aria-label="Editar y reenviar"
          onClick={onEditar}
        >
          {pencilIcon}
        </button>
      )}
    </div>
  );
}

/**
 * Copia la pregunta SIN ninguna etiqueta agregada («Vos:» nunca entra: no
 * está en el texto que este componente recibe, a diferencia de lo que se
 * vería en pantalla).
 */
function BotonDeCopiarPregunta({ texto, duracionMs }: { texto: string; duracionMs: number }) {
  const [copiado, setCopiado] = useState(false);
  const temporizador = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(temporizador.current), []);

  async function alPulsar() {
    try {
      await copiar(texto);
    } catch {
      // El navegador lo negó. El texto sigue ahí para seleccionarlo a mano;
      // el ícono no miente mostrando un tilde que no pasó.
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
      title={copiado ? "Copiado" : "Copiar pregunta"}
      aria-label={copiado ? "Copiado" : "Copiar pregunta"}
      onClick={() => void alPulsar()}
    >
      <span aria-hidden="true">{copiado ? checkIcon : copyIcon}</span>
    </button>
  );
}
