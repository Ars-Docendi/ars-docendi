import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaTareas } from "../components/TablaTareas";
import { EstadoProyectoBadge } from "../components/EstadoProyectoBadge";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { ModalProyecto } from "../components/ModalProyecto";
import { IconoArrowLeft, IconoPlus } from "../components/lucide";
import { formatearFecha } from "../components/detalleAdapters";
import { usePermisosTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useEstadosProyecto, useProyecto } from "../hooks/useProyectos";
import { useCrearTarea } from "../hooks/useAccionesTarea";
import { useCambiarEstadoProyecto, useEditarProyecto } from "../hooks/useAccionesProyecto";
import type { Tarea } from "../types";
import "./tareas.css";
import "../components/cuadroProyecto.css";

const RUTA_TAREAS = "/tareas";
const RUTA_PROYECTOS = "/tareas/proyectos";

export function DetalleProyectoPage() {
  const { id } = useParams();
  const navegar = useNavigate();
  const { data: proyecto, isLoading, isError } = useProyecto(id);
  const { data: tareas = [] } = useListadoTareas();
  const { data: estados = [] } = useEstadosProyecto();
  const cambiarEstado = useCambiarEstadoProyecto();
  const editarProyecto = useEditarProyecto();
  const crearTarea = useCrearTarea();
  const [modalTareaAbierto, setModalTareaAbierto] = useState(false);
  const [modalEditarAbierto, setModalEditarAbierto] = useState(false);

  const { puedeGestionarProyectos: puedeCambiar, puedeCrearTarea } = usePermisosTareas();
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
                {puedeCrearTarea && proyecto.admiteTareas && (
                  <Button
                    variant="primary"
                    leadingIcon={<IconoPlus />}
                    onClick={() => setModalTareaAbierto(true)}
                  >
                    Nueva Tarea
                  </Button>
                )}
                {puedeCambiar && (
                  <Button variant="secondary" onClick={() => setModalEditarAbierto(true)}>
                    Editar
                  </Button>
                )}
                {puedeCambiar &&
                  estados
                    .filter((estado) => estado.codigo !== proyecto.estado)
                    .filter(
                      (estado) =>
                        !(proyecto.estado === "cancelado" && estado.codigo === "finalizado"),
                    )
                    .map((estado) => (
                      <Button
                        key={estado.codigo}
                        variant={estado.admiteTareas ? "secondary" : "destructive"}
                        loading={cambiarEstado.isPending}
                        onClick={() =>
                          cambiarEstado.mutate({ id: proyecto.id, estadoDestino: estado.codigo })
                        }
                      >
                        {estado.verbo}
                      </Button>
                    ))}
                <EstadoProyectoBadge estado={proyecto.estado} nombre={proyecto.estadoNombre} />
              </div>
            }
          />

          {cambiarEstado.isError && (
            <InlineAlert severity="danger" title="No se pudo cambiar el estado del proyecto">
              {cambiarEstado.error.message}
            </InlineAlert>
          )}

          <div className="adoc-det-tarea">
            <section className="adoc-det-tarea-panel" aria-label="Datos del proyecto">
              <h3>Datos</h3>
              <dl className="adoc-det-tarea-datos">
                <div className="adoc-det-tarea-datos-fila">
                  <div className="adoc-det-tarea-dato">
                    <dt>Fecha de inicio</dt>
                    <dd>{formatearFecha(proyecto.fechaInicio)}</dd>
                  </div>
                  <div className="adoc-det-tarea-dato">
                    <dt>Fecha de fin</dt>
                    <dd>{formatearFecha(proyecto.fechaFin)}</dd>
                  </div>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>Responsable</dt>
                  <dd>
                    {proyecto.responsable.nombre} · {proyecto.responsable.rol}
                  </dd>
                </div>
              </dl>
            </section>

            <section className="adoc-det-tarea-panel" aria-label="Descripción del proyecto">
              <h2>Descripción</h2>
              <p>{proyecto.descripcion || "Sin descripción."}</p>
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

      {proyecto && (
        <ModalNuevaTarea
          open={modalTareaAbierto}
          proyectoFijo={proyecto}
          proyectos={[proyecto]}
          onCerrar={() => setModalTareaAbierto(false)}
          onGuardar={(datos) =>
            crearTarea.mutate({ datos }, { onSuccess: () => setModalTareaAbierto(false) })
          }
          guardando={crearTarea.isPending}
          error={crearTarea.isError ? crearTarea.error.message : undefined}
        />
      )}

      {proyecto && (
        <ModalProyecto
          open={modalEditarAbierto}
          proyecto={proyecto}
          onCerrar={() => setModalEditarAbierto(false)}
          onGuardar={(datos) =>
            editarProyecto.mutate(
              { id: proyecto.id, datos },
              { onSuccess: () => setModalEditarAbierto(false) },
            )
          }
          guardando={editarProyecto.isPending}
          error={editarProyecto.isError ? editarProyecto.error.message : undefined}
        />
      )}
    </>
  );
}
