import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Button, InlineAlert, Select } from "@ars-docendi/ui";
import { Migas } from "../../../shared/navegacion/Migas";
import { usePestanaURL } from "../../../shared/navegacion/estadoURL";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { CuadroProyecto } from "../components/CuadroProyecto";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { TablaTareas } from "../components/TablaTareas";
import { agruparTareasPorProyecto } from "../components/agrupacionProyectos";
import {
  ALCANCE_INICIAL,
  ETIQUETA_ALCANCE,
  aplicarAlcance,
  type AlcanceTareas,
} from "../components/alcanceTareas";
import { ETIQUETA_PRESET, PRESET_INICIAL, type PresetEstado } from "../components/presetEstado";

const PRESETS = Object.keys(ETIQUETA_PRESET) as PresetEstado[];
const ALCANCES = Object.keys(ETIQUETA_ALCANCE) as AlcanceTareas[];
/** Prefijo de los filtros de columna de la tabla única, en el query. */
const PREFIJO_TABLA = "t";
import { IconoPlus } from "../components/lucide";
import { useActorTareas, usePermisosTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useListadoProyectos } from "../hooks/useProyectos";
import { useCrearTarea } from "../hooks/useAccionesTarea";
import "./tareas.css";

export function IndexPage() {
  const { puedeCrearTarea } = usePermisosTareas();
  const actor = useActorTareas();
  const { data: tareas, isLoading, isError } = useListadoTareas();
  const { data: proyectos = [] } = useListadoProyectos();
  const crearTarea = useCrearTarea();

  // Preset y alcance viven en el query (defaults omitidos): Atrás y recargar los restauran.
  const [, setParams] = useSearchParams();
  const [preset] = usePestanaURL(PRESETS, PRESET_INICIAL, { nombre: "estado" });
  const [alcance, setAlcance] = usePestanaURL(ALCANCES, ALCANCE_INICIAL, {
    nombre: "alcance",
    reemplazar: true,
  });
  function elegirPreset(nuevo: PresetEstado) {
    setParams(
      (previos) => {
        const siguientes = new URLSearchParams(previos);
        if (nuevo === PRESET_INICIAL) siguientes.delete("estado");
        else siguientes.set("estado", nuevo);
        // Un preset nuevo recarga el filtro de la columna Estado.
        siguientes.delete(`${PREFIJO_TABLA}.estado`);
        return siguientes;
      },
      { replace: true },
    );
  }
  const [modalNuevaTareaAbierto, setModalNuevaTareaAbierto] = useState(false);

  const total = tareas?.length ?? 0;
  const visibles = aplicarAlcance(tareas ?? [], alcance, actor.id);
  const cuadros = agruparTareasPorProyecto(visibles, proyectos);

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Tareas" }]} />
      <PageHeader
        title="Tareas"
        meta={
          isLoading ? (
            "Cargando…"
          ) : (
            <>
              {total} tarea{total !== 1 ? "s" : ""}
            </>
          )
        }
        actions={
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
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
            <Select value={preset} onChange={(e) => elegirPreset(e.target.value as PresetEstado)}>
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
          prefijoURL={PREFIJO_TABLA}
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
    </>
  );
}
