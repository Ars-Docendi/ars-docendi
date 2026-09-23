import { useNavigate } from "react-router-dom";
import { TablaTareas } from "./TablaTareas";
import type { Proyecto, Tarea } from "../types";
import "./cuadroProyecto.css";

interface CuadroProyectoProps {
  /** `null` = el cuadro fijo "Generales" (tareas sin proyecto; su título no navega a ningún lado). */
  proyecto: Proyecto | null;
  tareas: Tarea[];
  onSeleccionarTarea: (tarea: Tarea) => void;
}

/**
 * Un cuadro de la pantalla inicial: título (nombre del Proyecto, o
 * "Generales") + la tabla de tareas de ese subconjunto — mismo
 * `TablaTareas` que antes, sin cambios en sus columnas. El título de un
 * cuadro de Proyecto navega a su Detalle; el de "Generales" no.
 */
export function CuadroProyecto({ proyecto, tareas, onSeleccionarTarea }: CuadroProyectoProps) {
  const navegar = useNavigate();

  return (
    <section className="adoc-cuadro-proyecto" aria-label={proyecto?.nombre ?? "Generales"}>
      {proyecto ? (
        <button
          type="button"
          className="adoc-cuadro-proyecto-titulo adoc-cuadro-proyecto-titulo--link"
          onClick={() => navegar(`/tareas/proyectos/${proyecto.id}`)}
        >
          {proyecto.nombre}
        </button>
      ) : (
        <h2 className="adoc-cuadro-proyecto-titulo">Generales</h2>
      )}
      <TablaTareas tareas={tareas} onSeleccionar={onSeleccionarTarea} />
    </section>
  );
}
