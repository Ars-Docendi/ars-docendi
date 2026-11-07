import { useState } from "react";
import { Link } from "react-router-dom";
import { Button, InlineAlert } from "@ars-docendi/ui";
import { propagarVolver } from "../../../shared/navegacion/volver";
import { useDestinoVolver } from "../../../shared/navegacion/useDestinoVolver";
import { ComboboxBuscable } from "../../../shared/ui/ComboboxBuscable";
import { Modal } from "../../../shared/ui/Modal";
import { useDesvincularHija, useVincularHija } from "../hooks/useAccionesTarea";
import { candidatasHija } from "./candidatasHija";
import { EstadoTareaBadge } from "./EstadoTareaBadge";
import { formatearFecha } from "./detalleAdapters";
import type { Tarea } from "../types";

interface TareasHijasProps {
  tarea: Tarea;
  /** Listado completo de tareas, para derivar las hijas directas. */
  todas: Tarea[];
  puedeCrear: boolean;
  /** Autoridad creadora de esta tarea: la única que vincula o desvincula hijas existentes. */
  puedeVincular: boolean;
  onCrearHija: () => void;
}

/**
 * Descomposición de una tarea compleja en tareas hijas — cada una con su
 * propio Estado/% de avance/Responsable; el avance del padre es el promedio
 * de ellas. Una hija puede a su vez tener sus propias hijas (se navega a su
 * Detalle para verlas). Se crean desde acá o se vinculan si ya existen.
 */
export function TareasHijas({
  tarea,
  todas,
  puedeCrear,
  puedeVincular,
  onCrearHija,
}: TareasHijasProps) {
  // El origen (la lista o el proyecto donde empezó el recorrido) viaja con cada tarea que se abre.
  const volver = useDestinoVolver("tarea");
  const hijas = todas.filter((t) => t.tareaPadreId === tarea.id);
  const candidatas = puedeVincular ? candidatasHija(tarea, todas) : [];
  const vincular = useVincularHija();
  const desvincular = useDesvincularHija();
  const enviando = vincular.isPending || desvincular.isPending;
  const error = vincular.error ?? desvincular.error;
  const [porConfirmar, setPorConfirmar] = useState<Tarea | null>(null);

  function confirmarVinculo(hija: Tarea) {
    setPorConfirmar(null);
    vincular.mutate({ id: tarea.id, hijaId: hija.id, version: hija.version });
  }

  function elegirCandidata(id: string) {
    const hija = candidatas.find((t) => t.id === id);
    if (!hija) return;
    // Sin Proyecto llega como null o undefined según la vía: no cuenta como un cambio.
    if ((hija.proyectoId ?? null) === (tarea.proyectoId ?? null)) confirmarVinculo(hija);
    else setPorConfirmar(hija);
  }

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
                {puedeVincular && (
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={enviando}
                    onClick={() =>
                      desvincular.mutate({ id: tarea.id, hijaId: hija.id, version: hija.version })
                    }
                    aria-label={`Desvincular "${hija.titulo}"`}
                  >
                    Desvincular
                  </Button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {candidatas.length > 0 && (
        <ComboboxBuscable
          key={hijas.length}
          valorSeleccionado=""
          opciones={candidatas.map((t) => ({
            value: t.id,
            label: `N° ${t.numero} — ${t.titulo}`,
          }))}
          placeholder="Buscar una tarea existente para vincular…"
          ariaLabel="Vincular una tarea existente"
          onSeleccionar={elegirCandidata}
        />
      )}

      {error && (
        <InlineAlert severity="danger" title="No se pudo actualizar las tareas hijas">
          {error.message}
        </InlineAlert>
      )}

      <Modal
        open={porConfirmar !== null}
        tamano="confirmacion"
        onOpenChange={() => setPorConfirmar(null)}
        title="Cambiar de proyecto"
        footer={
          <>
            <Button variant="secondary" onClick={() => setPorConfirmar(null)}>
              Cancelar
            </Button>
            <Button
              variant="primary"
              onClick={() => porConfirmar && confirmarVinculo(porConfirmar)}
            >
              Vincular
            </Button>
          </>
        }
      >
        <p style={{ margin: 0 }}>
          {tarea.proyectoId
            ? `"${porConfirmar?.titulo}" y sus tareas hijas van a pasar al proyecto de esta tarea.`
            : `"${porConfirmar?.titulo}" y sus tareas hijas van a quedar sin proyecto, como esta tarea.`}
        </p>
      </Modal>
    </section>
  );
}
