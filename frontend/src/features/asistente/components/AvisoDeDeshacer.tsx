import { useEffect, useRef } from "react";

import type { AvisoDeDeshacer as AvisoDeDeshacerModelo } from "../hooks/useHistorialAsistente";

interface AvisoDeDeshacerProps {
  aviso: AvisoDeDeshacerModelo;
  /**
   * A dónde va el foco si el aviso vence, o si «Deshacer» se lleva la fila,
   * mientras el foco seguía en el botón (asistente-accesibilidad).
   */
  enfocarLista: () => void;
}

/**
 * El aviso de deshacer al pie del rail: «Conversación archivada» /
 * «Conversación restaurada» / «Conversación eliminada» /
 * «Conversaciones eliminadas» + «Deshacer», visible 10 s
 * (asistente-superficie-frontend, design.md D4 de asistente-rediseno-v3).
 *
 * SIGUE VISIBLE CON EL RAIL COLAPSADO: `RailDeConversaciones` lo renderiza
 * afuera del bloque que el ancho colapsado esconde, y `asistente.css` lo
 * posiciona superpuesto al hilo en ese caso — el mock lo dibuja así, y el
 * texto tiene sentido igual sin ver la lista detrás.
 *
 * EL TEMPORIZADOR DE 10 S VIVE EN `useHistorialAsistente`, no acá: ese hook
 * es el que decide cuándo el aviso deja de existir (`aviso === null`), y
 * este componente sólo reacciona a que eso pase desmontándose. Lo que sí es
 * de este componente es la ORIENTACIÓN DEL FOCO alrededor de esa desaparición
 * (asistente-accesibilidad): foco a «Deshacer» al aparecer, y de vuelta a la
 * lista si el foco seguía acá cuando el aviso se fue —por vencer, o porque
 * «Deshacer» ya resolvió su trabajo y la fila restaurada todavía no está
 * lista para recibirlo—.
 */
export function AvisoDeDeshacer({ aviso, enfocarLista }: AvisoDeDeshacerProps) {
  const botonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    botonRef.current?.focus();

    return () => {
      // CONTRA `<body>`, NO CONTRA `botonRef.current`. Cuando el aviso VENCE
      // —el caso que este chequeo existe para cubrir—, React ya sacó el
      // botón del documento en el commit de desmontaje, ANTES de que este
      // efecto de limpieza corra: el navegador ya devolvió el foco a
      // `<body>` en ese mismo instante, así que comparar contra el nodo
      // desmontado siempre daría falso. Cuando en cambio una acción nueva
      // REEMPLAZA este aviso por otro, el componente no se desmonta —React
      // reusa el mismo botón— y el foco sigue genuinamente en él, nunca en
      // `<body>`, así que el chequeo no le pisa el foco a esa acción nueva.
      if (document.activeElement === document.body) {
        enfocarLista();
      }
    };
  }, [aviso, enfocarLista]);

  return (
    <div className="adoc-asistente-aviso" role="presentation">
      <span className="adoc-asistente-aviso-texto">{aviso.texto}</span>
      <button
        ref={botonRef}
        type="button"
        className="adoc-asistente-aviso-deshacer"
        onClick={() => void aviso.deshacer()}
      >
        Deshacer
      </button>
    </div>
  );
}
