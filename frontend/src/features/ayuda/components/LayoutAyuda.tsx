import { useState } from "react";
import { Outlet, useMatch, useSearchParams } from "react-router-dom";

import "../ayuda.css";
import { usePantallaAngosta } from "../hooks/usePantallaAngosta";
import { RUTA_AYUDA, SLUG_RESERVADO } from "../utils/rutas";
import { BarraLateralAyuda } from "./BarraLateralAyuda";
import { BuscadorAyuda } from "./BuscadorAyuda";
import type { ContextoLayoutAyuda } from "./contextoLayout";

/**
 * Layout de `/ayuda/*`: sidebar de documentación a la izquierda y la página activa
 * a la derecha. En pantallas angostas no hay sidebar: el buscador queda arriba del contenido
 * y se navega inicio → módulo → artículo. La contracción de la sidebar principal la deriva
 * `AppLayout` de la ruta.
 */
export function LayoutAyuda() {
  const [pasoActivo, fijarPasoActivo] = useState<string>();
  const angosta = usePantallaAngosta();
  const enBusqueda = useMatch(`${RUTA_AYUDA}/${SLUG_RESERVADO}`);
  const [parametros] = useSearchParams();
  const contexto: ContextoLayoutAyuda = { fijarPasoActivo };
  return (
    <div className="ayuda-layout">
      {!angosta && <BarraLateralAyuda pasoActivo={pasoActivo} />}
      <section className="ayuda-contenido" aria-label="Contenido de la ayuda">
        {angosta && (
          <div className="ayuda-buscador-superior">
            <BuscadorAyuda consultaInicial={enBusqueda ? (parametros.get("q") ?? "") : ""} />
          </div>
        )}
        <Outlet context={contexto} />
      </section>
    </div>
  );
}
