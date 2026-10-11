import { useNavigate } from "react-router-dom";
import { Button } from "@ars-docendi/ui";
import { ComboboxBuscable } from "../../../shared/ui/ComboboxBuscable";
import { formatearFecha } from "./detalleAdapters";
import type { Tarea } from "../types";

interface TareasRelacionadasProps {
  tarea: Tarea;
  /** Listado completo de tareas, para resolver ids a título y ofrecer candidatas a relacionar. */
  todas: Tarea[];
  onAgregar: (otraId: string) => void;
  onQuitar: (otraId: string) => void;
  enviando?: boolean;
}

/**
 * Acceso rápido bidireccional entre dos tareas (sin jerarquía, sin efecto
 * en Estado/% de avance). El buscador ofrece cualquier otra tarea que
 * todavía no esté relacionada; elegir una la relaciona de inmediato.
 */
export function TareasRelacionadas({
  tarea,
  todas,
  onAgregar,
  onQuitar,
  enviando = false,
}: TareasRelacionadasProps) {
  const navegar = useNavigate();

  const relacionadas = tarea.tareasRelacionadasIds
    .map((id) => todas.find((t) => t.id === id))
    .filter((t): t is Tarea => Boolean(t));

  const disponibles = todas.filter(
    (t) => t.id !== tarea.id && !tarea.tareasRelacionadasIds.includes(t.id),
  );

  return (
    <section className="adoc-det-tarea-panel" aria-label="Tareas relacionadas">
      <h2>Tareas relacionadas</h2>

      {relacionadas.length === 0 ? (
        <p>Todavía no hay tareas relacionadas.</p>
      ) : (
        <ul className="adoc-tareas-vinculo-lista">
          {relacionadas.map((otra) => (
            <li key={otra.id} className="adoc-tareas-vinculo-item">
              <button
                type="button"
                className="adoc-tareas-vinculo-link"
                onClick={() => navegar(`/tareas/${otra.id}`)}
              >
                N° {otra.numero} — {otra.titulo}
              </button>
              <span className="adoc-tareas-vinculo-info">
                <span className="adoc-tareas-vinculo-meta">
                  Fin {formatearFecha(otra.fechaFin)} · {otra.porcentajeAvance}%
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={enviando}
                  onClick={() => onQuitar(otra.id)}
                  aria-label={`Quitar la relación con "${otra.titulo}"`}
                >
                  Quitar
                </Button>
              </span>
            </li>
          ))}
        </ul>
      )}

      {disponibles.length > 0 && (
        <ComboboxBuscable
          valorSeleccionado=""
          opciones={disponibles.map((t) => ({
            value: t.id,
            label: `N° ${t.numero} — ${t.titulo}`,
          }))}
          placeholder="Buscar una tarea para relacionar…"
          ariaLabel="Relacionar con otra tarea"
          onSeleccionar={(id) => {
            if (id) onAgregar(id);
          }}
        />
      )}
    </section>
  );
}
