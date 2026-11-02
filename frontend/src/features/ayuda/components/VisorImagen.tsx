import { useEffect, useRef, useState } from "react";
import { Modal } from "@ars-docendi/ui";

import type { CapturaAyuda } from "../tipos";

/**
 * Captura de un paso: se muestra inline y se amplía a casi pantalla completa
 * (clic o teclado). Esc, el fondo y el botón de cerrar la cierran; el foco vuelve
 * a la captura que la abrió.
 *
 * Se usa el `Modal` de @ars-docendi/ui (no un `<dialog>` propio) por consistencia
 * con el resto de la app; no atrapa el foco ni lo devuelve solo, de eso se ocupa
 * este componente.
 */
export function VisorImagen({ captura }: { captura: CapturaAyuda }) {
  const [abierto, setAbierto] = useState(false);
  const disparador = useRef<HTMLButtonElement>(null);
  const cuerpo = useRef<HTMLDivElement>(null);

  // El Modal no mueve el foco al abrirse: se lo damos al diálogo para que el
  // lector de pantalla lo anuncie y Tab empiece dentro de él.
  useEffect(() => {
    if (!abierto) return;
    const dialogo = cuerpo.current?.closest<HTMLElement>('[role="dialog"]');
    dialogo?.setAttribute("tabindex", "-1");
    dialogo?.focus();
    // El foco vuelve a la captura al cerrar. Va diferido porque, al cerrar con el
    // fondo, la acción por defecto del clic sacaría el foco justo después.
    const miniatura = disparador.current;
    return () => {
      setTimeout(() => miniatura?.focus(), 0);
    };
  }, [abierto]);

  return (
    <figure className="ayuda-captura">
      <button
        type="button"
        ref={disparador}
        className="ayuda-captura-miniatura"
        aria-label={`Ampliar captura: ${captura.alt}`}
        onClick={() => setAbierto(true)}
      >
        <img src={captura.src} alt={captura.alt} loading="lazy" />
      </button>
      {captura.leyenda && <figcaption>{captura.leyenda}</figcaption>}
      <Modal
        open={abierto}
        onOpenChange={setAbierto}
        aria-label={captura.alt}
        className="ayuda-visor"
      >
        <div ref={cuerpo} className="ayuda-visor-cuerpo">
          <img src={captura.src} alt={captura.alt} />
          {captura.leyenda && <p>{captura.leyenda}</p>}
        </div>
      </Modal>
    </figure>
  );
}
