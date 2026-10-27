import { useRef, useState } from "react";

import { helpIcon } from "../../../app/shell/icons";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useDescartarAlClicAfuera } from "../../../shared/hooks/useDescartarAlClicAfuera";

/** Qué es el asistente, hasta dónde llega y qué no hace, detrás de un «?». */
export function AyudaDelAsistente() {
  const { capacidades } = useAccesoAlAsistente();
  const [abierta, setAbierta] = useState(false);
  const contenedor = useRef<HTMLDivElement>(null);

  useDescartarAlClicAfuera(abierta, contenedor, () => setAbierta(false));

  // Sin catálogo no hay nada que contar. Un «?» que se abre vacío es peor que no
  // tenerlo: promete una explicación que no llega.
  if (!capacidades) return null;

  return (
    <div className="adoc-asistente-ayuda" ref={contenedor}>
      <button
        type="button"
        className="adoc-asistente-ayuda-boton"
        aria-label="Qué puede y qué no puede hacer el asistente"
        aria-expanded={abierta}
        onClick={() => setAbierta((estaba) => !estaba)}
      >
        <span className="ico">{helpIcon}</span>
      </button>

      {abierta && (
        <div className="adoc-asistente-ayuda-pop" role="group" aria-label="Sobre el asistente">
          <p className="adoc-asistente-inicio-presentacion">{capacidades.presentacion}</p>
          <p className="adoc-asistente-inicio-alcance">
            {capacidades.alcance} Conozco {areasDeDatos(capacidades.tablas)} del sistema.
          </p>

          {capacidades.noPuede.length > 0 && (
            // Sin límites no queda ni el rótulo.
            <div className="adoc-asistente-inicio-detalle">
              <p className="adoc-asistente-inicio-rotulo">No puedo:</p>
              <ul className="adoc-asistente-inicio-lista">
                {capacidades.noPuede.map((limite) => (
                  <li key={limite}>{limite}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** «1 área de datos», «2 áreas de datos». */
function areasDeDatos(cantidad: number): string {
  return cantidad === 1 ? "1 área de datos" : `${cantidad} áreas de datos`;
}
