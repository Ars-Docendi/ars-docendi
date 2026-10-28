import { useState } from "react";
import { Outlet } from "react-router-dom";

import "../ayuda.css";
import { BarraLateralAyuda } from "./BarraLateralAyuda";
import type { ContextoLayoutAyuda } from "./contextoLayout";

/**
 * Layout de `/ayuda/*`: sidebar de documentación a la izquierda y la página activa
 * a la derecha. La contracción de la sidebar principal la deriva `AppLayout` de la ruta.
 */
export function LayoutAyuda() {
  const [pasoActivo, fijarPasoActivo] = useState<string>();
  const contexto: ContextoLayoutAyuda = { fijarPasoActivo };
  return (
    <div className="ayuda-layout">
      <BarraLateralAyuda pasoActivo={pasoActivo} />
      <section className="ayuda-contenido" aria-label="Contenido de la ayuda">
        <Outlet context={contexto} />
      </section>
    </div>
  );
}
