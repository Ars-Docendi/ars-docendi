import { useResaltado } from "../../../shared/ui/resaltado";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AuditLog, Button, InlineAlert, TrafficLight } from "@ars-docendi/ui";
import { EnlaceVolver } from "../../../shared/navegacion/EnlaceVolver";
import { Migas } from "../../../shared/navegacion/Migas";
import { useDestinoVolver } from "../../../shared/navegacion/useDestinoVolver";
import {
  migaDeVolver,
  propagarVolver,
  type DestinoVolver,
} from "../../../shared/navegacion/volver";
import { BarraAccionesFija } from "../../../shared/ui/BarraAccionesFija";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { usePantallaTelefono } from "../../../shared/ui/usePantallaTelefono";
import { EstadoTareaBadge } from "../components/EstadoTareaBadge";
import { AccionesEstadoTarea } from "../components/AccionesEstadoTarea";
import { ComentariosTarea } from "../components/ComentariosTarea";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { TareasRelacionadas } from "../components/TareasRelacionadas";
import { TareasHijas } from "../components/TareasHijas";
import { IconoBan, IconoSquarePen } from "../components/lucide";
import { formatearFecha, historialAAuditEntries } from "../components/detalleAdapters";
import { estadoSemaforo, muestraSemaforo } from "../components/semaforoTarea";
import { puedeCambiarEstado, puedeEditarCampos } from "../api/maquinaEstadosTarea";
import { useActorTareas, usePermisosTareas } from "../hooks/useActorTareas";
import { useListadoTareas, useTarea } from "../hooks/useTareas";
import { useListadoProyectos } from "../hooks/useProyectos";
import {
  useAgregarComentario,
  useAgregarRelacion,
  useCambiarEstadoTarea,
  useCrearTarea,
  useEditarAvance,
  useEditarTarea,
  useQuitarRelacion,
} from "../hooks/useAccionesTarea";
import type {
  ActorTarea,
  DatosEditablesTarea,
  EstadoTarea,
  Proyecto,
  Tarea,
  TipoTarea,
} from "../types";
import "./tareas.css";
import { Modal } from "../../../shared/ui/Modal";

const ETIQUETA_PRIORIDAD: Record<Tarea["prioridad"], string> = {
  alta: "Alta",
  media: "Media",
  baja: "Baja",
};

const ETIQUETA_TIPO: Record<TipoTarea, string> = {
  extension: "Extensión",
  administrativa: "Administrativa",
  posgrado: "Posgrado",
  investigacion: "Investigación",
  academica: "Académicas",
  decanato: "Decanato",
};

export function DetalleTareaPage() {
  const { id } = useParams();
  const volver = useDestinoVolver("tarea");
  const actor = useActorTareas();
  const { data: tarea, isLoading, isError } = useTarea(id);
  const { data: todas = [] } = useListadoTareas();
  const { data: proyectos = [] } = useListadoProyectos();

  const cambiarEstado = useCambiarEstadoTarea();
  const editarAvance = useEditarAvance();
  const editarTarea = useEditarTarea();
  const agregarComentario = useAgregarComentario();
  const crearTarea = useCrearTarea();
  const agregarRelacion = useAgregarRelacion();
  const quitarRelacion = useQuitarRelacion();
  const enviando = cambiarEstado.isPending || editarAvance.isPending;

  return (
    <>
      <Migas
        conVolver
        items={[
          { label: "Inicio", href: "/" },
          migaDeVolver(volver),
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
          No pudimos cargar la tarea solicitada. <Link to={volver.ruta}>{volver.texto}</Link>.
        </InlineAlert>
      )}

      {tarea && (
        <DetalleCargado
          tarea={tarea}
          actor={actor}
          todas={todas}
          proyectos={proyectos}
          enviando={enviando}
          errorEstado={cambiarEstado.isError ? cambiarEstado.error.message : undefined}
          volver={volver}
          onCambiarEstado={(estadoDestino, opciones) =>
            cambiarEstado.mutate({
              id: tarea.id,
              version: tarea.version,
              estadoDestino,
              ...opciones,
            })
          }
          onCancelar={(onSuccess) =>
            cambiarEstado.mutate(
              { id: tarea.id, version: tarea.version, estadoDestino: "cancelada" },
              { onSuccess },
            )
          }
          onEditarAvance={(porcentajeAvance) =>
            editarAvance.mutate({ id: tarea.id, version: tarea.version, porcentajeAvance })
          }
          onEditar={(datos, onSuccess, version) =>
            editarTarea.mutate({ id: tarea.id, version, datos }, { onSuccess })
          }
          editando={editarTarea.isPending}
          errorEditar={editarTarea.isError ? editarTarea.error.message : undefined}
          onAgregarComentario={(texto) => agregarComentario.mutate({ id: tarea.id, texto })}
          comentando={agregarComentario.isPending}
          onCrearHija={(datos, onSuccess) =>
            crearTarea.mutate({ datos, tareaPadreId: tarea.id }, { onSuccess })
          }
          creandoHija={crearTarea.isPending}
          errorCrearHija={crearTarea.isError ? crearTarea.error.message : undefined}
          onAgregarRelacion={(otraId) => agregarRelacion.mutate({ id: tarea.id, otraId })}
          onQuitarRelacion={(otraId) => quitarRelacion.mutate({ id: tarea.id, otraId })}
          enviandoRelacion={agregarRelacion.isPending || quitarRelacion.isPending}
        />
      )}
    </>
  );
}

interface DetalleCargadoProps {
  tarea: Tarea;
  actor: ActorTarea;
  todas: Tarea[];
  proyectos: Proyecto[];
  enviando: boolean;
  errorEstado?: string;
  volver: DestinoVolver;
  onCambiarEstado: (
    estadoDestino: EstadoTarea,
    opciones?: { comentario?: string; solucion?: string },
  ) => void;
  onEditarAvance: (porcentajeAvance: number) => void;
  onCancelar: (onSuccess: () => void) => void;
  onEditar: (datos: DatosEditablesTarea, onSuccess: () => void, version: number) => void;
  editando: boolean;
  errorEditar?: string;
  onAgregarComentario: (texto: string) => void;
  comentando: boolean;
  onCrearHija: (datos: DatosEditablesTarea, onSuccess: () => void) => void;
  creandoHija: boolean;
  errorCrearHija?: string;
  onAgregarRelacion: (otraId: string) => void;
  onQuitarRelacion: (otraId: string) => void;
  enviandoRelacion: boolean;
}

function DetalleCargado({
  tarea,
  actor,
  todas,
  proyectos,
  enviando,
  errorEstado,
  volver,
  onCambiarEstado,
  onEditarAvance,
  onCancelar,
  onEditar,
  editando,
  errorEditar,
  onAgregarComentario,
  comentando,
  onCrearHija,
  creandoHija,
  errorCrearHija,
  onAgregarRelacion,
  onQuitarRelacion,
  enviandoRelacion,
}: DetalleCargadoProps) {
  const resaltada = useResaltado(tarea.id);
  const telefono = usePantallaTelefono();
  const [modalEditarAbierto, setModalEditarAbierto] = useState(false);
  const [versionEditar, setVersionEditar] = useState(tarea.version);
  const [modalCancelarAbierto, setModalCancelarAbierto] = useState(false);
  const [modalHijaAbierto, setModalHijaAbierto] = useState(false);

  const puedeEditar = puedeEditarCampos(tarea, actor);
  const puedeCancelar = puedeCambiarEstado(tarea, actor, "cancelada");
  const { puedeCrearTarea: puedeCrearHija } = usePermisosTareas();

  const proyecto = tarea.proyectoId ? proyectos.find((p) => p.id === tarea.proyectoId) : undefined;
  const padre = tarea.tareaPadreId ? todas.find((t) => t.id === tarea.tareaPadreId) : undefined;

  const acciones = (
    <>
      {puedeEditar && (
        <Button
          variant="secondary"
          leadingIcon={<IconoSquarePen />}
          onClick={() => {
            setVersionEditar(tarea.version);
            setModalEditarAbierto(true);
          }}
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
    </>
  );

  return (
    <>
      <PageHeader
        title={tarea.titulo}
        meta={`Prioridad ${ETIQUETA_PRIORIDAD[tarea.prioridad]} · Responsable ${tarea.responsable.nombre}`}
        actions={
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            <EnlaceVolver id="tarea-volver" destino={volver} />
            {!telefono && acciones}
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

            <TareasHijas
              tarea={tarea}
              todas={todas}
              puedeCrear={puedeCrearHija}
              onCrearHija={() => setModalHijaAbierto(true)}
            />

            <TareasRelacionadas
              tarea={tarea}
              todas={todas}
              onAgregar={onAgregarRelacion}
              onQuitar={onQuitarRelacion}
              enviando={enviandoRelacion}
            />

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

            <section
              className={`adoc-det-tarea-panel${resaltada ? " adoc-resaltado" : ""}`}
              aria-label="Datos de la tarea"
              data-entidad-id={tarea.id}
            >
              <h3>Datos</h3>
              <dl className="adoc-det-tarea-datos">
                <div className="adoc-det-tarea-dato">
                  <dt>Tipo</dt>
                  <dd>{ETIQUETA_TIPO[tarea.tipo]}</dd>
                </div>
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
                <div className="adoc-det-tarea-dato">
                  <dt>Proyecto</dt>
                  <dd>
                    {proyecto ? (
                      <Link
                        className="adoc-tareas-vinculo-link"
                        to={`/tareas/proyectos/${proyecto.id}`}
                        state={propagarVolver(volver)}
                      >
                        {proyecto.nombre}
                      </Link>
                    ) : (
                      "Sin proyecto"
                    )}
                  </dd>
                </div>
                {padre && (
                  <div className="adoc-det-tarea-dato">
                    <dt>Tarea padre</dt>
                    <dd>
                      <Link
                        className="adoc-tareas-vinculo-link"
                        to={`/tareas/${padre.id}`}
                        state={propagarVolver(volver)}
                      >
                        N° {padre.numero} — {padre.titulo}
                      </Link>
                    </dd>
                  </div>
                )}
              </dl>
            </section>
          </aside>
        </div>
      </div>

      {telefono && (puedeEditar || puedeCancelar) && (
        <BarraAccionesFija>{acciones}</BarraAccionesFija>
      )}

      <ModalNuevaTarea
        open={modalEditarAbierto}
        tarea={tarea}
        proyectos={proyectos}
        onCerrar={() => setModalEditarAbierto(false)}
        onGuardar={(datos) => onEditar(datos, () => setModalEditarAbierto(false), versionEditar)}
        guardando={editando}
        error={errorEditar}
      />

      <ModalNuevaTarea
        open={modalHijaAbierto}
        tareaPadre={tarea}
        proyectos={proyectos}
        onCerrar={() => setModalHijaAbierto(false)}
        onGuardar={(datos) => onCrearHija(datos, () => setModalHijaAbierto(false))}
        guardando={creandoHija}
        error={errorCrearHija}
      />

      <Modal
        open={modalCancelarAbierto}
        tamano="confirmacion"
        pendiente={enviando}
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
