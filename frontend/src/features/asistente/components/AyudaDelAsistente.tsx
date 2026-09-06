import { useEffect, useRef, useState } from "react";

import { helpIcon } from "../../../app/shell/icons";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";

/**
 * Qué es el asistente, hasta dónde llega y qué no hace, detrás de un «?».
 *
 * ES UN POPOVER Y NO UN TOOLTIP DE HOVER, y la diferencia importa acá. Lo que
 * guarda es un párrafo de presentación, la línea de alcance y una lista de cuatro
 * límites: un tooltip de hover no se puede leer a esa extensión —se cierra al ir
 * hacia él con el puntero—, no se alcanza con teclado y no se puede dejar abierto
 * mientras se lee lo de al lado. Se abre con clic, se cierra con Escape o con un
 * clic afuera, que es el patrón que `MenuAcciones` ya estableció en el proyecto.
 *
 * POR QUÉ SE SACÓ DE LA PANTALLA. Ocupaba la mitad del estado inicial y competía
 * con lo único accionable que hay ahí: los ejemplos. Quien abre el asistente por
 * décima vez no vuelve a leer los límites, y quien lo abre por primera vez tampoco
 * los lee antes de probar. Sigue disponible porque cuando hace falta —«¿por qué no
 * me contestó esto?»— es exactamente lo que responde.
 *
 * VIVE EN EL ENCABEZADO Y NO EN LA PANTALLA VACÍA, así que sigue disponible después
 * del primer turno — que es cuando aparece la pregunta que responde. Pide el
 * catálogo por su cuenta: `useAccesoAlAsistente` es la misma consulta de React
 * Query que ya hizo el panel, así que no es un pedido más.
 *
 * El texto lo escribe el backend y acá no se inventa nada.
 */
export function AyudaDelAsistente() {
  const { capacidades } = useAccesoAlAsistente();
  const [abierta, setAbierta] = useState(false);
  const contenedor = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!abierta) return;

    function alApuntar(evento: MouseEvent) {
      if (contenedor.current && !contenedor.current.contains(evento.target as Node)) {
        setAbierta(false);
      }
    }

    function alTeclear(evento: KeyboardEvent) {
      if (evento.key === "Escape") setAbierta(false);
    }

    document.addEventListener("mousedown", alApuntar);
    document.addEventListener("keydown", alTeclear);

    return () => {
      document.removeEventListener("mousedown", alApuntar);
      document.removeEventListener("keydown", alTeclear);
    };
  }, [abierta]);

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
