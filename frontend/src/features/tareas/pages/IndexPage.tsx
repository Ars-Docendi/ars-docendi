import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert, Select } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { CuadroProyecto } from "../components/CuadroProyecto";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { ModalNuevoProyecto } from "../components/ModalNuevoProyecto";
import { TablaTareas } from "../components/TablaTareas";
import { agruparTareasPorProyecto } from "../components/agrupacionProyectos";
import {
  ALCANCE_INICIAL,
  ETIQUETA_ALCANCE,
  aplicarAlcance,
  type AlcanceTareas,
} from "../components/alcanceTareas";
import { ETIQUETA_PRESET, PRESET_INICIAL, type PresetEstado } from "../components/presetEstado";
import { IconoPlus } from "../components/lucide";
import { useActorTareas, usePermisosTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useListadoProyectos } from "../hooks/useProyectos";
import { useCrearTarea } from "../hooks/useAccionesTarea";
import { useCrearProyecto } from "../hooks/useAccionesProyecto";
import type { Tarea } from "../types";
import "./tareas.css";

export function IndexPage() {
  const navegar = useNavigate();
  const { puedeCrearTarea, puedeGestionarProyectos } = usePermisosTareas();
  const actor = useActorTareas();
  const { data: tareas, isLoading, isError } = useListadoTareas();
  const { data: proyectos = [] } = useListadoProyectos();
  const crearTarea = useCrearTarea();
  const crearProyecto = useCrearProyecto();

  const [preset, setPreset] = useState<PresetEstado>(PRESET_INICIAL);
  const [alcance, setAlcance] = useState<AlcanceTareas>(ALCANCE_INICIAL);
  const [modalNuevaTareaAbierto, setModalNuevaTareaAbierto] = useState(false);
  const [modalNuevoProyectoAbierto, setModalNuevoProyectoAbierto] = useState(false);

  const total = tareas?.length ?? 0;
  const visibles = aplicarAlcance(tareas ?? [], alcance, actor.id);
  const cuadros = agruparTareasPorProyecto(visibles, proyectos);

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
              {total} tarea{total !== 1 ? "s" : ""}
              {puedeCrearTarea && (
                <>
                  {" · "}
                  <button
                    type="button"
                    className="adoc-tareas-vinculo-link"
                    onClick={() => navegar("/tareas/proyectos")}
                  >
                    Ver todos los proyectos
                  </button>
                </>
              )}
            </>
          )
        }
        actions={
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            {puedeGestionarProyectos && (
              <Button variant="secondary" onClick={() => setModalNuevoProyectoAbierto(true)}>
                Nuevo Proyecto
              </Button>
            )}
            {puedeCrearTarea && (
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

      {!isLoading && !isError && total > 0 && (
        <div className="adoc-preset-estado" role="group" aria-label="Filtros del listado">
          <label>
            Estado:{" "}
            <Select value={preset} onChange={(e) => setPreset(e.target.value as PresetEstado)}>
              {(Object.keys(ETIQUETA_PRESET) as PresetEstado[]).map((opcion) => (
                <option key={opcion} value={opcion}>
                  {ETIQUETA_PRESET[opcion]}
                </option>
              ))}
            </Select>
          </label>
          {puedeCrearTarea && (
            <label>
              Tareas:{" "}
              <Select value={alcance} onChange={(e) => setAlcance(e.target.value as AlcanceTareas)}>
                {(Object.keys(ETIQUETA_ALCANCE) as AlcanceTareas[]).map((opcion) => (
                  <option key={opcion} value={opcion}>
                    {ETIQUETA_ALCANCE[opcion]}
                  </option>
                ))}
              </Select>
            </label>
          )}
        </div>
      )}

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando las tareas…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar las tareas">
          Hubo un problema al obtener el listado de tareas. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no hay tareas cargadas">
          {puedeCrearTarea
            ? 'Empezá creando la primera tarea con "Nueva Tarea".'
            : "Cuando una autoridad cree una tarea, la vas a ver acá."}
        </InlineAlert>
      )}

      {!isLoading && !isError && total > 0 && !puedeCrearTarea && (
        <TablaTareas
          tareas={visibles}
          onSeleccionar={(tarea: Tarea) => navegar(`/tareas/${tarea.id}`)}
          preset={preset}
          proyectos={proyectos}
        />
      )}

      {!isLoading &&
        !isError &&
        total > 0 &&
        puedeCrearTarea &&
        cuadros.map((cuadro) => (
          <CuadroProyecto
            key={cuadro.proyecto?.id ?? "generales"}
            proyecto={cuadro.proyecto}
            tareas={cuadro.tareas}
            onSeleccionarTarea={(tarea: Tarea) => navegar(`/tareas/${tarea.id}`)}
            preset={preset}
          />
        ))}

      <ModalNuevaTarea
        open={modalNuevaTareaAbierto}
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
