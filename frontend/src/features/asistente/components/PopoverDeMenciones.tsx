import { fileTextIcon, lockIcon, personIcon } from "../../../app/shell/icons";
import type { ResultadoDeMencion, TipoDeMencion } from "../types";

interface PopoverDeMencionesProps {
  id: string;
  tipo: TipoDeMencion;
  resultados: ResultadoDeMencion[];
  hayMas: boolean;
  activo: number;
  idDeOpcion: (indice: number) => string;
  onSeleccionar: (resultado: ResultadoDeMencion) => void;
}

/**
 * El popover de menciones «@materia» / «#docente» sobre el composer
 * (asistente-menciones, design.md D10 de asistente-rediseno-v3): un grupo, sus
 * filas y el pie con el candado. Vive por completo detrás del combobox del
 * campo — `aria-activedescendant` mueve la fila activa desde ahí, y ESTE
 * componente no toma foco propio nunca (asistente-accesibilidad).
 *
 * `onMouseDown`, NO `onClick`, en cada fila: con `onClick` el campo pierde el
 * foco un instante antes —el `blur` del `mousedown`— y quien esté siguiendo
 * la posición del cursor para la próxima tecla lo pierde. Elegir por clic
 * tiene que dejar el foco donde lo dejan las flechas y Enter.
 */
export function PopoverDeMenciones({
  id,
  tipo,
  resultados,
  hayMas,
  activo,
  idDeOpcion,
  onSeleccionar,
}: PopoverDeMencionesProps) {
  const esMateria = tipo === "materia";

  return (
    <div className="adoc-asistente-menciones-popover" id={id}>
      <div className="adoc-asistente-menciones-grupo" aria-hidden="true">
        {esMateria ? "MATERIAS" : "DOCENTES"}
      </div>

      {resultados.length === 0 ? (
        <p className="adoc-asistente-menciones-vacio">
          {esMateria
            ? "Sin materias que coincidan en las carreras a las que tenés acceso."
            : "Sin docentes que coincidan en las carreras a las que tenés acceso."}
        </p>
      ) : (
        <div
          className="adoc-asistente-menciones-lista"
          role="listbox"
          aria-label={esMateria ? "Materias" : "Docentes"}
        >
          {resultados.map((resultado, indice) => (
            <button
              key={resultado.id}
              type="button"
              id={idDeOpcion(indice)}
              role="option"
              aria-selected={indice === activo}
              className={
                indice === activo
                  ? "adoc-asistente-mencion-fila adoc-asistente-mencion-fila--activa"
                  : "adoc-asistente-mencion-fila"
              }
              onMouseDown={(evento) => {
                evento.preventDefault();
                onSeleccionar(resultado);
              }}
            >
              <span className="adoc-asistente-mencion-icono" aria-hidden="true">
                {esMateria ? fileTextIcon : personIcon}
              </span>
              <span className="adoc-asistente-mencion-nombre">{resultado.nombre}</span>
              <span className="adoc-asistente-mencion-datos">
                {esMateria ? (
                  <>
                    <span className="adoc-asistente-mencion-pastilla">{resultado.carrera}</span>
                    <span className="adoc-asistente-mencion-codigo">{resultado.codigo}</span>
                  </>
                ) : (
                  <span className="adoc-asistente-mencion-pastilla">{resultado.cargo}</span>
                )}
              </span>
            </button>
          ))}
        </div>
      )}

      {hayMas && (
        <p className="adoc-asistente-menciones-capped">
          Hay más coincidencias. Seguí escribiendo para acotar.
        </p>
      )}

      <div className="adoc-asistente-menciones-pie">
        <span className="adoc-asistente-mencion-candado" aria-hidden="true">
          {lockIcon}
        </span>
        <span className="adoc-asistente-menciones-alcance">
          Solo aparecen materias y docentes de las carreras a las que tu perfil tiene acceso.
        </span>
        <span className="adoc-asistente-menciones-atajos">Enter elige · Esc cierra</span>
      </div>
    </div>
  );
}
