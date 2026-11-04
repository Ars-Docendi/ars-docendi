import { useState, type ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import { Button, InlineAlert, Select } from "@ars-docendi/ui";
import { Migas } from "../../../shared/navegacion/Migas";
import { usePestanaURL } from "../../../shared/navegacion/estadoURL";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { PanelPestana, Pestanas } from "../../../shared/ui/Pestanas";
import { usePantallaTelefono } from "../../../shared/ui/usePantallaTelefono";
import { CuadroProyecto } from "../components/CuadroProyecto";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { TablaTareas } from "../components/TablaTareas";
import { agruparTareasPorProyecto, prefijoDeCuadro } from "../components/agrupacionProyectos";
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
  const telefono = usePantallaTelefono();

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
        // Un preset nuevo recarga el filtro de la columna Estado de la tabla única y de cada cuadro.
        for (const clave of [...siguientes.keys()]) {
          if (clave === `${PREFIJO_TABLA}.estado` || /^(g|p[0-9a-f]{32})\.estado$/.test(clave)) {
            siguientes.delete(clave);
          }
        }
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

      {!isLoading && !isError && total > 0 && (!telefono || puedeCrearTarea) && (
        <div className="adoc-preset-estado" role="group" aria-label="Filtros del listado">
          {!telefono && (
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
          )}
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

      {!isLoading && !isError && total > 0 && telefono && (
        <Pestanas
          prefijo="tareas-estado"
          aria-label="Estado de las tareas"
          items={PRESETS.map((opcion) => ({ id: opcion, label: ETIQUETA_PRESET[opcion] }))}
          value={preset}
          onChange={(id) => elegirPreset(id as PresetEstado)}
        />
      )}

      {!isLoading && !isError && total > 0 && (
        <ContenidoPreset telefono={telefono} preset={preset}>
          {puedeCrearTarea ? (
            cuadros.map((cuadro) => (
              <CuadroProyecto
                key={cuadro.proyecto?.id ?? "generales"}
                proyecto={cuadro.proyecto}
                tareas={cuadro.tareas}
                preset={preset}
                prefijoURL={prefijoDeCuadro(cuadro.proyecto)}
              />
            ))
          ) : (
            <TablaTareas
              tareas={visibles}
              prefijoURL={PREFIJO_TABLA}
              preset={preset}
              proyectos={proyectos}
            />
          )}
        </ContenidoPreset>
      )}

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

/** On phones the tab strip labels the content it filters; on desktop there is no wrapper. */
function ContenidoPreset({
  telefono,
  preset,
  children,
}: {
  telefono: boolean;
  preset: PresetEstado;
  children: ReactNode;
}) {
  return telefono ? (
    <PanelPestana prefijo="tareas-estado" id={preset}>
      {children}
    </PanelPestana>
  ) : (
    <>{children}</>
  );
}
