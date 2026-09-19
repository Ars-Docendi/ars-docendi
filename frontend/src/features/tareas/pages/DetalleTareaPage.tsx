import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { AuditLog, Breadcrumbs, Button, InlineAlert, Modal, TrafficLight } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { EstadoTareaBadge } from "../components/EstadoTareaBadge";
import { AccionesEstadoTarea } from "../components/AccionesEstadoTarea";
import { ComentariosTarea } from "../components/ComentariosTarea";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { IconoArrowLeft, IconoBan, IconoSquarePen } from "../components/lucide";
import { formatearFecha, historialAAuditEntries } from "../components/detalleAdapters";
import { estadoSemaforo, muestraSemaforo } from "../components/semaforoTarea";
import { puedeCambiarEstado, puedeEditarCampos } from "../api/maquinaEstadosTarea";
import { useActorTareas } from "../hooks/useActorTareas";
import { useTarea } from "../hooks/useTareas";
import {
  useAgregarComentario,
  useCambiarEstadoTarea,
  useEditarAvance,
  useEditarTarea,
} from "../hooks/useAccionesTarea";
import type { ActorTarea, DatosEditablesTarea, EstadoTarea, Tarea } from "../types";
import "./tareas.css";

const RUTA_TAREAS = "/tareas";

const ETIQUETA_PRIORIDAD: Record<Tarea["prioridad"], string> = {
  alta: "Alta",
  media: "Media",
  baja: "Baja",
};

export function DetalleTareaPage() {
  const { id } = useParams();
  const navegar = useNavigate();
  const actor = useActorTareas();
  const { data: tarea, isLoading, isError } = useTarea(id);

  const cambiarEstado = useCambiarEstadoTarea(actor);
  const editarAvance = useEditarAvance(actor);
  const editarTarea = useEditarTarea(actor);
  const agregarComentario = useAgregarComentario(actor);
  const enviando = cambiarEstado.isPending || editarAvance.isPending;

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Tareas", href: RUTA_TAREAS },
          { label: "Detalle de la tarea" },
        ]}
      />

      {isLoading && (
        <p role="status" aria-live="polite" style={{ color: "var(--color-text-secondary)" }}>
          Cargando la tarea…
        </p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se encontró la tarea">
          No pudimos cargar la tarea solicitada. <a href={RUTA_TAREAS}>Volver a Tareas</a>.
        </InlineAlert>
      )}

      {tarea && (
        <DetalleCargado
          tarea={tarea}
          actor={actor}
          enviando={enviando}
          errorEstado={cambiarEstado.isError ? cambiarEstado.error.message : undefined}
          onVolver={() => navegar(-1)}
          onCambiarEstado={(estadoDestino, opciones) =>
            cambiarEstado.mutate({ id: tarea.id, estadoDestino, ...opciones })
          }
          onCancelar={(onSuccess) =>
            cambiarEstado.mutate({ id: tarea.id, estadoDestino: "cancelada" }, { onSuccess })
          }
          onEditarAvance={(porcentajeAvance) =>
            editarAvance.mutate({ id: tarea.id, porcentajeAvance })
          }
          onEditar={(datos, onSuccess) =>
            editarTarea.mutate({ id: tarea.id, datos }, { onSuccess })
          }
          editando={editarTarea.isPending}
          errorEditar={editarTarea.isError ? editarTarea.error.message : undefined}
          onAgregarComentario={(texto) => agregarComentario.mutate({ id: tarea.id, texto })}
          comentando={agregarComentario.isPending}
        />
      )}
    </>
  );
}

interface DetalleCargadoProps {
  tarea: Tarea;
  actor: ActorTarea;
  enviando: boolean;
  errorEstado?: string;
  onVolver: () => void;
  onCambiarEstado: (
    estadoDestino: EstadoTarea,
    opciones?: { comentario?: string; solucion?: string },
  ) => void;
  onEditarAvance: (porcentajeAvance: number) => void;
  onCancelar: (onSuccess: () => void) => void;
  onEditar: (datos: DatosEditablesTarea, onSuccess: () => void) => void;
  editando: boolean;
  errorEditar?: string;
  onAgregarComentario: (texto: string) => void;
  comentando: boolean;
}

function DetalleCargado({
  tarea,
  actor,
  enviando,
  errorEstado,
  onVolver,
  onCambiarEstado,
  onEditarAvance,
  onCancelar,
  onEditar,
  editando,
  errorEditar,
  onAgregarComentario,
  comentando,
}: DetalleCargadoProps) {
  const [modalEditarAbierto, setModalEditarAbierto] = useState(false);
  const [modalCancelarAbierto, setModalCancelarAbierto] = useState(false);

  const puedeEditar = puedeEditarCampos(tarea, actor);
  const puedeCancelar = puedeCambiarEstado(tarea, actor, "cancelada");

  return (
    <>
      <PageHeader
        pretitle={`Tareas · Tarea N° ${tarea.numero}`}
        title={tarea.titulo}
        meta={`Prioridad ${ETIQUETA_PRIORIDAD[tarea.prioridad]} · Responsable ${tarea.responsable.nombre}`}
        actions={
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            <Button variant="secondary" leadingIcon={<IconoArrowLeft />} onClick={onVolver}>
              Volver
            </Button>
            {puedeEditar && (
              <Button
                variant="secondary"
                leadingIcon={<IconoSquarePen />}
                onClick={() => setModalEditarAbierto(true)}
              >
                Editar
              </Button>
            )}
            {puedeCancelar && (
              <Button
                variant="destructive"
                leadingIcon={<IconoBan />}
                onClick={() => setModalCancelarAbierto(true)}
              >
                Cancelar
              </Button>
            )}
            <EstadoTareaBadge estado={tarea.estado} />
          </div>
        }
      />

      <div className="adoc-det-tarea">
        <div className="adoc-det-tarea-cols">
          <div className="adoc-det-tarea-main">
            <section className="adoc-det-tarea-panel" aria-label="Descripción">
              <h2>Descripción</h2>
              <p>{tarea.descripcion || "Sin descripción."}</p>
            </section>

            {tarea.solucion && (
              <section className="adoc-det-tarea-panel" aria-label="Solución">
                <h2>Solución</h2>
                <p>{tarea.solucion}</p>
              </section>
            )}

            <ComentariosTarea
              comentarios={tarea.comentarios}
              onAgregar={onAgregarComentario}
              enviando={comentando}
            />

            <section className="adoc-det-tarea-panel" aria-label="Historial de la tarea">
              <h2>Historial</h2>
              <AuditLog entries={historialAAuditEntries(tarea.historial)} />
            </section>
          </div>

          <aside className="adoc-det-tarea-rail">
            <AccionesEstadoTarea
              tarea={tarea}
              actor={actor}
              enviando={enviando}
              error={errorEstado}
              onCambiarEstado={onCambiarEstado}
              onEditarAvance={onEditarAvance}
            />

            <section className="adoc-det-tarea-panel" aria-label="Datos de la tarea">
              <h3>Datos</h3>
              <dl className="adoc-det-tarea-datos">
                <div className="adoc-det-tarea-dato">
                  <dt>Fecha de inicio</dt>
                  <dd>{formatearFecha(tarea.fechaInicio)}</dd>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>Fecha de fin</dt>
                  <dd>
                    {muestraSemaforo(tarea.estado) ? (
                      <TrafficLight
                        state={estadoSemaforo(tarea.fechaInicio, tarea.fechaFin)}
                        due={formatearFecha(tarea.fechaFin)}
                      />
                    ) : (
                      formatearFecha(tarea.fechaFin)
                    )}
                  </dd>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>% de avance</dt>
                  <dd>{tarea.porcentajeAvance}%</dd>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>Responsable</dt>
                  <dd>
                    {tarea.responsable.nombre} · {tarea.responsable.rol}
                  </dd>
                </div>
                <div className="adoc-det-tarea-dato">
                  <dt>Autor</dt>
                  <dd>
                    {tarea.creadoPor.nombre} · {tarea.creadoPor.rol}
                  </dd>
                </div>
              </dl>
            </section>
          </aside>
        </div>
      </div>

      <ModalNuevaTarea
        open={modalEditarAbierto}
        tarea={tarea}
        onCerrar={() => setModalEditarAbierto(false)}
        onGuardar={(datos) => onEditar(datos, () => setModalEditarAbierto(false))}
        guardando={editando}
        error={errorEditar}
      />

      <Modal
        open={modalCancelarAbierto}
        onOpenChange={setModalCancelarAbierto}
        title="Cancelar tarea"
        footer={
          <>
            <Button variant="secondary" onClick={() => setModalCancelarAbierto(false)}>
              Volver
            </Button>
            <Button
              variant="destructive"
              onClick={() => onCancelar(() => setModalCancelarAbierto(false))}
              loading={enviando}
            >
              Cancelar tarea
            </Button>
          </>
        }
      >
        <p>
          ¿Confirmás cancelar la tarea "{tarea.titulo}"? Esta acción queda registrada en el
          historial y solo vos, como autoridad creadora, podés reabrirla después.
        </p>
        {errorEstado && (
          <div style={{ marginTop: "12px" }}>
            <InlineAlert severity="danger" title="No se pudo cancelar la tarea">
              {errorEstado}
            </InlineAlert>
          </div>
        )}
      </Modal>
    </>
  );
}
