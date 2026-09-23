import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { CuadroProyecto } from "../components/CuadroProyecto";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { ModalNuevoProyecto } from "../components/ModalNuevoProyecto";
import { agruparTareasPorProyecto } from "../components/agrupacionProyectos";
import { IconoPlus } from "../components/lucide";
import { puedeCrearTarea } from "../api/maquinaEstadosTarea";
import { puedeCrearProyecto } from "../api/maquinaEstadosProyecto";
import { useActorTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useListadoProyectos } from "../hooks/useProyectos";
import { useCrearTarea } from "../hooks/useAccionesTarea";
import { useCrearProyecto } from "../hooks/useAccionesProyecto";
import type { Tarea } from "../types";
import "./tareas.css";

export function IndexPage() {
  const navegar = useNavigate();
  const actor = useActorTareas();
  const { data: tareas, isLoading, isError } = useListadoTareas();
  const { data: proyectos = [] } = useListadoProyectos();
  const crearTarea = useCrearTarea(actor);
  const crearProyecto = useCrearProyecto(actor);

  const [modalNuevaTareaAbierto, setModalNuevaTareaAbierto] = useState(false);
  const [modalNuevoProyectoAbierto, setModalNuevoProyectoAbierto] = useState(false);

  const total = tareas?.length ?? 0;
  const cuadros = agruparTareasPorProyecto(tareas ?? [], proyectos);

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Tareas" }]} />
      <PageHeader
        pretitle="Cuatrimestre 2026 · 1C"
        title="Tareas"
        meta={
          isLoading ? (
            "Cargando…"
          ) : (
            <>
              {total} tarea{total !== 1 ? "s" : ""} ·{" "}
              <button
                type="button"
                className="adoc-tareas-vinculo-link"
                onClick={() => navegar("/tareas/proyectos")}
              >
                Ver todos los proyectos
              </button>
            </>
          )
        }
        actions={
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            {puedeCrearProyecto(actor) && (
              <Button variant="secondary" onClick={() => setModalNuevoProyectoAbierto(true)}>
                Nuevo Proyecto
              </Button>
            )}
            {puedeCrearTarea(actor) && (
              <Button
                variant="primary"
                leadingIcon={<IconoPlus />}
                onClick={() => setModalNuevaTareaAbierto(true)}
              >
                Nueva Tarea
              </Button>
            )}
          </div>
        }
      />

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando las tareas…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar las tareas">
          Hubo un problema al obtener el listado de tareas. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no hay tareas cargadas">
          {puedeCrearTarea(actor)
            ? 'Empezá creando la primera tarea con "Nueva Tarea".'
            : "Cuando una autoridad cree una tarea, la vas a ver acá."}
        </InlineAlert>
      )}

      {!isLoading &&
        !isError &&
        total > 0 &&
        cuadros.map((cuadro) => (
          <CuadroProyecto
            key={cuadro.proyecto?.id ?? "generales"}
            proyecto={cuadro.proyecto}
            tareas={cuadro.tareas}
            onSeleccionarTarea={(tarea: Tarea) => navegar(`/tareas/${tarea.id}`)}
          />
        ))}

      <ModalNuevaTarea
        open={modalNuevaTareaAbierto}
        actor={actor}
        proyectos={proyectos}
        onCerrar={() => setModalNuevaTareaAbierto(false)}
        onGuardar={(datos) => {
          crearTarea.mutate({ datos }, { onSuccess: () => setModalNuevaTareaAbierto(false) });
        }}
        guardando={crearTarea.isPending}
        error={crearTarea.isError ? crearTarea.error.message : undefined}
      />

      <ModalNuevoProyecto
        open={modalNuevoProyectoAbierto}
        actor={actor}
        onCerrar={() => setModalNuevoProyectoAbierto(false)}
        onGuardar={(datos) => {
          crearProyecto.mutate(datos, { onSuccess: () => setModalNuevoProyectoAbierto(false) });
        }}
        guardando={crearProyecto.isPending}
        error={crearProyecto.isError ? crearProyecto.error.message : undefined}
      />
    </>
  );
}
