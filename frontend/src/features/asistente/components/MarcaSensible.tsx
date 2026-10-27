import { lockIcon } from "../../../app/shell/icons";

/**
 * La marca de una columna que trae un dato personal. Que el valor no pasó por el modelo y viene
 * directo del motor es mecánica interna (RNF-18): al usuario le importa saber qué columna es un
 * dato personal, no cómo se lo protegió.
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
