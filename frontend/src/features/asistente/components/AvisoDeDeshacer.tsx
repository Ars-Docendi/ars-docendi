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
 * El aviso de deshacer al pie del rail: «Conversación archivada» / «Conversación restaurada» /
 * «Conversación eliminada» / «Conversaciones eliminadas» + «Deshacer», visible 10 s
 * (asistente-superficie-frontend, design.md D4 de asistente-rediseno-v3).
 */
export function AvisoDeDeshacer({ aviso, enfocarLista }: AvisoDeDeshacerProps) {
  const botonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    botonRef.current?.focus();

    return () => {
      // CONTRA `<body>`, NO CONTRA `botonRef.current`.
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
