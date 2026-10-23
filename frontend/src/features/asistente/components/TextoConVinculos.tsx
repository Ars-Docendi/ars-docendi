import { Link } from "react-router-dom";

import { fileTextIcon } from "../../../app/shell/icons";
import { destinoDe } from "../utils/destinos";
import type { VinculoDelTexto } from "../types";

interface TextoConVinculosProps {
  texto: string;
  vinculos: VinculoDelTexto[];
}

/**
 * El texto de la respuesta, con cada elemento vinculado como un chip en línea
 * (asistente-respuestas-con-vinculos, D1 y D8).
 *
 * EL TEXTO SE PINTA TAL CUAL LLEGÓ. Las posiciones del backend son índices de
 * UTF-16, los mismos de `String.prototype.slice`; recortar o normalizar el texto
 * las desalinearía. `white-space: pre-wrap` (`.adoc-asistente-texto`) se conserva
 * porque los chips son `<a>` en línea dentro del mismo párrafo.
 *
 * LA POSICIÓN SE VALIDA ANTES DE PINTAR: dentro del texto, sin superponerse con la
 * anterior, y con un tipo que este cliente conozca. Una posición que no cumple se
 * deja como texto: es preferible no enlazar a enlazar lo que no es.
 */
export function TextoConVinculos({ texto, vinculos }: TextoConVinculosProps) {
  const partes: React.ReactNode[] = [];
  let cursor = 0;

  [...vinculos]
    .sort((a, b) => a.desde - b.desde)
    .forEach((vinculo, indice) => {
      const { desde, largo } = vinculo;
      const destino = destinoDe(vinculo.tipo);
      const valida =
        destino !== undefined &&
        Number.isInteger(desde) &&
        Number.isInteger(largo) &&
        largo > 0 &&
        desde >= cursor &&
        desde + largo <= texto.length;
      if (!valida) return;

      const valor = texto.slice(desde, desde + largo);
      if (desde > cursor) partes.push(texto.slice(cursor, desde));

      partes.push(
        <Link
          key={`${vinculo.tipo}-${vinculo.id}-${indice}`}
          className="adoc-asistente-chip"
          to={destino.ruta(vinculo.id)}
          aria-label={`Ver el ${destino.sustantivo} ${valor}`}
          data-vinculo-del-asistente=""
        >
          <span className="adoc-asistente-chip-icono" aria-hidden="true">
            {fileTextIcon}
          </span>
          {valor}
        </Link>,
      );
      cursor = desde + largo;
    });

  if (cursor < texto.length) partes.push(texto.slice(cursor));

  return <p className="adoc-asistente-texto">{partes}</p>;
}
