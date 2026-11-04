import { forwardRef } from "react";

import { etiquetaCampana, textoInsignia } from "../presentacion";
import { IconoCampana } from "./iconos";

interface CampanaProps {
  /** `undefined` while the first count is loading: the bell shows no badge. */
  cantidad: number | undefined;
  abierta: boolean;
  onClick: () => void;
}

/**
 * Sidebar bell (desktop), between "Centro de ayuda" and the user card. Expanded it shows the label
 * and a count pill; collapsed, a badge on the bell's corner and a tooltip.
 */
export const CampanaSidebar = forwardRef<HTMLButtonElement, CampanaProps & { colapsada: boolean }>(
  function CampanaSidebar({ cantidad, abierta, colapsada, onClick }, ref) {
    const n = cantidad ?? 0;
    return (
      <button
        ref={ref}
        type="button"
        id="campana-notificaciones"
        className="adoc-sidebar-bell"
        aria-label={etiquetaCampana(n)}
        aria-haspopup="dialog"
        aria-expanded={abierta}
        data-tooltip={colapsada ? "Notificaciones" : undefined}
        onClick={onClick}
      >
        <span className="ico">
          <IconoCampana />
          {n > 0 && (
            <span className="adoc-notif-insignia" data-testid="insignia-esquina">
              {textoInsignia(n)}
            </span>
          )}
        </span>
        {!colapsada && <span className="etiqueta">Notificaciones</span>}
        {n > 0 && (
          <span className="pildora" data-testid="insignia-pildora">
            {textoInsignia(n)}
          </span>
        )}
      </button>
    );
  },
);

/** Top-bar bell (narrow screens): the icon with a corner badge. */
export const CampanaTopBar = forwardRef<HTMLButtonElement, CampanaProps>(function CampanaTopBar(
  { cantidad, abierta, onClick },
  ref,
) {
  const n = cantidad ?? 0;
  return (
    <button
      ref={ref}
      type="button"
      id="campana-notificaciones-movil"
      className="adoc-campana"
      aria-label={etiquetaCampana(n)}
      aria-haspopup="dialog"
      aria-expanded={abierta}
      onClick={onClick}
    >
      <span className="ico">
        <IconoCampana />
        {n > 0 && (
          <span className="adoc-notif-insignia" data-testid="insignia-esquina">
            {textoInsignia(n)}
          </span>
        )}
      </span>
    </button>
  );
});
