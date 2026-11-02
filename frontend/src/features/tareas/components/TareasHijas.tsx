import { Link } from "react-router-dom";
import { Button } from "@ars-docendi/ui";
import { propagarVolver } from "../../../shared/navegacion/volver";
import { useDestinoVolver } from "../../../shared/navegacion/useDestinoVolver";
import { EstadoTareaBadge } from "./EstadoTareaBadge";
import { formatearFecha } from "./detalleAdapters";
import type { Tarea } from "../types";

interface TareasHijasProps {
  tarea: Tarea;
  /** Listado completo de tareas, para derivar las hijas directas. */
  todas: Tarea[];
  puedeCrear: boolean;
  onCrearHija: () => void;
}

/**
 * Descomposición de una tarea compleja en tareas hijas independientes —
 * cada una con su propio Estado/% de avance/Responsable, sin rollup
 * automático hacia el padre. Una hija puede a su vez tener sus propias
 * hijas (se navega a su Detalle para verlas).
 */
export function TareasHijas({ tarea, todas, puedeCrear, onCrearHija }: TareasHijasProps) {
  // El origen (la lista o el proyecto donde empezó el recorrido) viaja con cada tarea que se abre.
  const volver = useDestinoVolver("tarea");
  const hijas = todas.filter((t) => t.tareaPadreId === tarea.id);

  return (
    <section className="adoc-det-tarea-panel" aria-label="Tareas hijas">
      <div className="adoc-tareas-hijas-cabecera">
        <h2>Tareas hijas</h2>
        {puedeCrear && (
          <Button variant="secondary" size="sm" onClick={onCrearHija}>
            Nueva tarea hija
          </Button>
        )}
      </div>

      {hijas.length === 0 ? (
        <p>Esta tarea todavía no tiene tareas hijas.</p>
      ) : (
        <ul className="adoc-tareas-vinculo-lista">
          {hijas.map((hija) => (
            <li key={hija.id} className="adoc-tareas-vinculo-item">
              <Link
                className="adoc-tareas-vinculo-link"
                to={`/tareas/${hija.id}`}
                state={propagarVolver(volver)}
              >
                N° {hija.numero} — {hija.titulo}
              </Link>
              <span className="adoc-tareas-vinculo-info">
                <span className="adoc-tareas-vinculo-meta">
                  Fin {formatearFecha(hija.fechaFin)} · {hija.porcentajeAvance}%
                </span>
                <EstadoTareaBadge estado={hija.estado} />
                <span>{hija.responsable.nombre}</span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
