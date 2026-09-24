import { useNavigate, useParams } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaTareas } from "../components/TablaTareas";
import { EstadoProyectoBadge } from "../components/EstadoProyectoBadge";
import { IconoArrowLeft, IconoBan, IconoCircleCheck } from "../components/lucide";
import { formatearFecha } from "../components/detalleAdapters";
import { puedeCambiarEstadoProyecto } from "../api/maquinaEstadosProyecto";
import { useActorTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useProyecto } from "../hooks/useProyectos";
import { useCambiarEstadoProyecto } from "../hooks/useAccionesProyecto";
import type { Tarea } from "../types";
import "./tareas.css";
import "../components/cuadroProyecto.css";

const RUTA_TAREAS = "/tareas";
const RUTA_PROYECTOS = "/tareas/proyectos";

export function DetalleProyectoPage() {
  const { id } = useParams();
  const navegar = useNavigate();
  const actor = useActorTareas();
  const { data: proyecto, isLoading, isError } = useProyecto(id);
  const { data: tareas = [] } = useListadoTareas();
  const cambiarEstado = useCambiarEstadoProyecto(actor);

  const puedeCambiar = puedeCambiarEstadoProyecto(actor);
  const tareasDelProyecto = proyecto
    ? tareas.filter((t: Tarea) => t.proyectoId === proyecto.id)
    : [];

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Tareas", href: RUTA_TAREAS },
          { label: "Proyectos", href: RUTA_PROYECTOS },
          { label: "Detalle del proyecto" },
        ]}
      />

      {isLoading && (
        <p role="status" aria-live="polite" style={{ color: "var(--color-text-secondary)" }}>
          Cargando el proyecto…
        </p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se encontró el proyecto">
          No pudimos cargar el proyecto solicitado. <a href={RUTA_PROYECTOS}>Volver a Proyectos</a>.
        </InlineAlert>
      )}

      {proyecto && (
        <>
          <PageHeader
            pretitle="Tareas · Proyecto"
            title={proyecto.nombre}
            meta={`Responsable ${proyecto.responsable.nombre} · ${proyecto.responsable.rol}`}
            actions={
              <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                <Button
                  variant="secondary"
                  leadingIcon={<IconoArrowLeft />}
                  onClick={() => navegar(RUTA_PROYECTOS)}
                >
                  Volver
                </Button>
                {puedeCambiar && proyecto.estado === "abierto" && (
                  <>
                    <Button
                      variant="secondary"
                      leadingIcon={<IconoCircleCheck />}
                      loading={cambiarEstado.isPending}
                      onClick={() =>
                        cambiarEstado.mutate({ id: proyecto.id, estadoDestino: "finalizado" })
                      }
                    >
                      Finalizar
                    </Button>
                    <Button
                      variant="destructive"
                      leadingIcon={<IconoBan />}
                      loading={cambiarEstado.isPending}
                      onClick={() =>
                        cambiarEstado.mutate({ id: proyecto.id, estadoDestino: "cancelado" })
                      }
                    >
                      Cancelar
                    </Button>
                  </>
                )}
                <EstadoProyectoBadge estado={proyecto.estado} />
              </div>
            }
          />

          {cambiarEstado.isError && (
            <InlineAlert severity="danger" title="No se pudo cambiar el estado del proyecto">
              {cambiarEstado.error.message}
            </InlineAlert>
          )}

          <div className="adoc-det-tarea">
            <section className="adoc-det-tarea-panel" aria-label="Descripción del proyecto">
              <h2>Descripción</h2>
              <p>{proyecto.descripcion || "Sin descripción."}</p>
            </section>

            <section className="adoc-det-tarea-panel" aria-label="Datos del proyecto">
              <h3>Datos</h3>
              <dl className="adoc-det-tarea-datos">
                <div className="adoc-det-tarea-dato">
                  <dt>Fecha de inicio</dt>
                  <dd>{formatearFecha(proyecto.fechaInicio)}</dd>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>Fecha de fin</dt>
                  <dd>{formatearFecha(proyecto.fechaFin)}</dd>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>Responsable</dt>
                  <dd>
                    {proyecto.responsable.nombre} · {proyecto.responsable.rol}
                  </dd>
                </div>
              </dl>
            </section>

            <section aria-label="Tareas del proyecto">
              <h2 className="adoc-cuadro-proyecto-titulo">Tareas</h2>
              {tareasDelProyecto.length === 0 ? (
                <p style={{ color: "var(--color-text-secondary)" }}>
                  Este proyecto todavía no tiene tareas asociadas.
                </p>
              ) : (
                <TablaTareas
                  tareas={tareasDelProyecto}
                  onSeleccionar={(tarea) => navegar(`/tareas/${tarea.id}`)}
                />
              )}
            </section>
          </div>
        </>
      )}
    </>
  );
}
