import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  FiltrosLista,
  type CampoFiltroFijo,
  type CampoFiltroOpcional,
} from "../../../shared/ui/FiltrosLista";
import { TablaTareas } from "../components/TablaTareas";
import { ConfiguracionesFiltro } from "../components/ConfiguracionesFiltro";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { IconoPlus } from "../components/lucide";
import {
  aplicarFiltrosTareas,
  FILTROS_INICIALES,
  type FiltrosTareasState,
} from "../components/filtrosTareas";
import { ORDEN_INICIAL, ordenarTareas, siguienteOrden } from "../components/ordenTareas";
import { PERSONAS_CANDIDATAS } from "../api/personasSeed";
import { puedeCrearTarea } from "../api/maquinaEstadosTarea";
import { useActorTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useCrearTarea } from "../hooks/useAccionesTarea";
import { useFiltrosGuardados, useGuardarFiltros } from "../hooks/useFiltrosGuardados";
import type { Tarea } from "../types";
import type { ConfiguracionFiltro } from "../api/filtrosGuardadosStore";
import "./tareas.css";

const OPCIONES_RESPONSABLE = PERSONAS_CANDIDATAS.map((p) => ({ value: p.nombre, label: p.nombre }));

const FILTROS_FIJOS: CampoFiltroFijo[] = [
  { clave: "numero", placeholder: "Filtrar por N°…", ariaLabel: "Filtrar por N°", ancho: "chica" },
  {
    clave: "responsable",
    ariaLabel: "Filtrar por responsable",
    tipo: "buscable",
    placeholder: "Buscar responsable…",
    opciones: OPCIONES_RESPONSABLE,
  },
  { clave: "titulo", placeholder: "Filtrar por título…", ariaLabel: "Filtrar por título" },
];

const FILTROS_OPCIONALES: CampoFiltroOpcional[] = [
  { tipo: "texto", clave: "autor", etiqueta: "Autor", placeholder: "Autor…" },
  {
    tipo: "multiSelect",
    clave: "estado",
    etiqueta: "Estado",
    etiquetaTodos: "Estado: Todos",
    opciones: [
      { value: "pendiente", label: "Pendiente" },
      { value: "en_curso", label: "En curso" },
      { value: "pausa", label: "Pausa" },
      { value: "resuelta", label: "Resuelta" },
      { value: "cancelada", label: "Cancelada" },
    ],
  },
  {
    tipo: "select",
    clave: "prioridad",
    etiqueta: "Prioridad",
    valorInicial: "todos",
    opciones: [
      { value: "todos", label: "Prioridad: Todas" },
      { value: "alta", label: "Alta" },
      { value: "media", label: "Media" },
      { value: "baja", label: "Baja" },
    ],
  },
  {
    tipo: "numero",
    clave: "avance",
    etiqueta: "% Avance",
    placeholder: "% Avance…",
    min: 0,
    max: 100,
  },
  { tipo: "fecha", clave: "fechaInicio", etiqueta: "Fecha Inicio" },
  { tipo: "fecha", clave: "fechaFin", etiqueta: "Fecha Fin" },
];

export function IndexPage() {
  const navegar = useNavigate();
  const actor = useActorTareas();
  const { data: tareas, isLoading, isError } = useListadoTareas();
  const crear = useCrearTarea(actor);
  const { data: configuraciones } = useFiltrosGuardados(actor);
  const guardarFiltros = useGuardarFiltros(actor);

  const [filtros, setFiltros] = useState<FiltrosTareasState>(FILTROS_INICIALES);
  const [orden, setOrden] = useState(ORDEN_INICIAL);
  const [modalNuevaAbierto, setModalNuevaAbierto] = useState(false);

  const total = tareas?.length ?? 0;
  const filtradas = tareas ? aplicarFiltrosTareas(tareas, filtros) : [];
  const ordenadas = ordenarTareas(filtradas, orden);

  function aplicarConfiguracion(config: ConfiguracionFiltro) {
    setFiltros(config.filtros);
  }

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Tareas" }]} />
      <PageHeader
        pretitle="Cuatrimestre 2026 · 1C"
        title="Tareas"
        meta={isLoading ? "Cargando…" : `${total} tarea${total !== 1 ? "s" : ""}`}
        actions={
          puedeCrearTarea(actor) ? (
            <Button
              variant="primary"
              leadingIcon={<IconoPlus />}
              onClick={() => setModalNuevaAbierto(true)}
            >
              Nueva Tarea
            </Button>
          ) : undefined
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

      {!isLoading && !isError && total > 0 && (
        <div className="adoc-tareas-section">
          <div className="adoc-tareas-filtros-fila">
            <FiltrosLista
              fijos={FILTROS_FIJOS}
              opcionales={FILTROS_OPCIONALES}
              valores={filtros}
              onChange={setFiltros}
            />
            <ConfiguracionesFiltro
              configuraciones={configuraciones ?? []}
              onAplicar={aplicarConfiguracion}
              onGuardar={(nombre) => guardarFiltros.mutate({ nombre, filtros })}
              guardando={guardarFiltros.isPending}
            />
          </div>

          {filtradas.length === 0 ? (
            <InlineAlert severity="info" title="Sin resultados">
              Ninguna tarea coincide con los filtros aplicados.
            </InlineAlert>
          ) : (
            <TablaTareas
              tareas={ordenadas}
              orden={orden}
              onOrdenar={(clave) => setOrden((actual) => siguienteOrden(actual, clave))}
              onSeleccionar={(tarea: Tarea) => navegar(`/tareas/${tarea.id}`)}
            />
          )}
        </div>
      )}

      <ModalNuevaTarea
        open={modalNuevaAbierto}
        onCerrar={() => setModalNuevaAbierto(false)}
        onGuardar={(datos) => {
          crear.mutate(datos, { onSuccess: () => setModalNuevaAbierto(false) });
        }}
        guardando={crear.isPending}
        error={crear.isError ? crear.error.message : undefined}
      />
    </>
  );
}
