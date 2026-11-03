import { Link } from "react-router-dom";
import { useEstadoVolver } from "../../../shared/navegacion/useEstadoVolver";
import { TablaTareas } from "./TablaTareas";
import type { PresetEstado } from "./presetEstado";
import type { Proyecto, Tarea } from "../types";
import "./cuadroProyecto.css";

interface CuadroProyectoProps {
  /** `null` = el cuadro fijo "Generales" (tareas sin proyecto; su título no navega a ningún lado). */
  proyecto: Proyecto | null;
  tareas: Tarea[];
  preset?: PresetEstado;
  /** Prefijo de los filtros y el orden de columna de este cuadro en el query (ver `prefijoDeCuadro`). */
  prefijoURL?: string;
}

/**
 * Un cuadro de la pantalla inicial: título (nombre del Proyecto, o
 * "Generales") + la tabla de tareas de ese subconjunto — mismo
 * `TablaTareas` que antes, sin cambios en sus columnas. El título de un
 * cuadro de Proyecto navega a su Detalle; el de "Generales" no.
 */
export function CuadroProyecto({ proyecto, tareas, preset, prefijoURL }: CuadroProyectoProps) {
  const estado = useEstadoVolver();

  return (
    <section className="adoc-cuadro-proyecto" aria-label={proyecto?.nombre ?? "Generales"}>
      {proyecto ? (
        <Link
          to={`/tareas/proyectos/${proyecto.id}`}
          state={estado}
          className="adoc-cuadro-proyecto-titulo adoc-cuadro-proyecto-titulo--link"
        >
          {proyecto.nombre}
        </Link>
      ) : (
        <h2 className="adoc-cuadro-proyecto-titulo">Generales</h2>
      )}
      <TablaTareas tareas={tareas} preset={preset} prefijoURL={prefijoURL} />
    </section>
  );
}
