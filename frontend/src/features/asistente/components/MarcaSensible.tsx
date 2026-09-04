import { lockIcon } from "../../../app/shell/icons";

/**
 * La marca de una columna que trae un dato personal.
 *
 * El candado es para quien ve y «(dato personal)» para quien escucha: las dos cosas
 * en la misma cabecera, para que el lector de pantalla lo diga al pasar por la
 * columna y no haya que ir a buscar una leyenda aparte. El ícono va oculto al
 * lector porque el texto ya lo dice; anunciarlo dos veces es ruido.
 *
 * Dice QUÉ es sensible y no por dónde viajó. Que el valor no pasó por el modelo y
 * viene directo del motor es mecánica interna (RNF-18): al usuario le importa saber
 * qué columna es un dato personal, no cómo se lo protegió.
 */
export function MarcaSensible() {
  return (
    <span className="adoc-asistente-sensible">
      <span className="adoc-asistente-candado" aria-hidden="true">
        {lockIcon}
      </span>
      {/* El espacio inicial separa «documento» de «(dato personal)» en el nombre
          accesible; sin él, el lector los lee pegados. */}
      <span className="adoc-sr"> (dato personal)</span>
    </span>
  );
}
