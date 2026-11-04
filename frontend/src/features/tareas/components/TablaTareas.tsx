import { useMemo, useState } from "react";
import { Button, Input, Table } from "@ars-docendi/ui";
import { EnlaceFila } from "../../../shared/navegacion/EnlaceFila";
import { useFiltrosURL, useOrdenURL } from "../../../shared/navegacion/estadoURL";
import { useAbrirDetalle } from "../../../shared/navegacion/useEstadoVolver";
import { ContenedorTabla } from "../../../shared/ui/ContenedorTabla";
import { usePantallaTelefono } from "../../../shared/ui/usePantallaTelefono";
import { propsFilaClickeable } from "../../../shared/ui/filaClickeable";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import type { EstadoTarea, Prioridad, Proyecto, Tarea } from "../types";
import { EstadoTareaBadge } from "./EstadoTareaBadge";
import { nivelAlertaSemaforo } from "./semaforoTarea";
import { formatearFecha } from "./detalleAdapters";
import {
  alternarOpcion,
  ETIQUETA_ESTADO,
  ETIQUETA_PRIORIDAD,
  aplicarFiltrosColumnas,
  FILTROS_COLUMNAS_INICIALES,
  opcionesColumnasTareas,
  type FiltrosColumnasTareas,
} from "./filtrosTareas";
import {
  ordenarTareas,
  siguienteOrden,
  type ColumnaOrdenableTarea,
  type OrdenTareas,
} from "./ordenTareas";
import { ListaTareasMovil } from "./ListaTareasMovil";
import { OpcionesFiltro } from "./OpcionesFiltro";
import { estadosDePreset, type PresetEstado } from "./presetEstado";
import "./tablaTareas.css";

interface TablaTareasProps {
  tareas: Tarea[];
  /**
   * Si se pasa, filtros y orden de columnas viven en el query con este prefijo (y sobreviven a
   * Atrás y a recargar). Sin prefijo —varias tablas en una página— quedan en memoria.
   */
  prefijoURL?: string;
  /** Filtro preseleccionado: al cambiarlo se carga el filtro de la columna Estado (que sigue editable). */
  preset?: PresetEstado;
  /**
   * Si se pasa, se agrega la columna Proyecto (para quien ve sus tareas en una sola tabla, sin
   * cuadros por proyecto): muestra el nombre del proyecto de cada tarea.
   */
  proyectos?: Proyecto[];
}

/** Columnas ordenables y su rótulo, en el orden en que se muestran. */
const COLUMNAS: { id: ColumnaOrdenableTarea; etiqueta: string }[] = [
  { id: "numero", etiqueta: "N°" },
  { id: "titulo", etiqueta: "Título" },
  { id: "autor", etiqueta: "Autor" },
  { id: "responsable", etiqueta: "Responsable" },
  { id: "fechaInicio", etiqueta: "Inicio" },
  { id: "fechaFin", etiqueta: "Fin" },
  { id: "prioridad", etiqueta: "Prioridad" },
  { id: "avance", etiqueta: "% Avance" },
  { id: "estado", etiqueta: "Estado" },
];

/**
 * Listado único de tareas — mismo modelo que
 * `designaciones/components/TablaRevision.tsx`: el `Table` del design
 * system, un filtro por columna en su propio header (`FiltroEncabezado`,
 * texto libre o checkboxes según la columna) y orden por header con ciclo
 * asc → desc → default (Fecha Inicio ascendente). El semáforo de
 * vencimiento colorea el fondo de toda la fila (amarillo/rojo; verde no se
 * resalta), y el estado de Pausa se distingue con su propio tono de badge.
 */
export function TablaTareas({ tareas, preset, proyectos, prefijoURL }: TablaTareasProps) {
  const abrir = useAbrirDetalle();
  const telefono = usePantallaTelefono();
  // El filtro Estado arranca con lo que indica el preset; la URL sólo guarda lo que se aparta.
  const porDefecto = useMemo<FiltrosColumnasTareas>(
    () => ({
      ...FILTROS_COLUMNAS_INICIALES,
      estado: preset ? estadosDePreset(preset) : [],
    }),
    [preset],
  );
  const [filtrosURL, setFiltrosURL] = useFiltrosURL(porDefecto, prefijoURL);
  const [ordenURL, setOrdenURL] = useOrdenURL(COLUMNAS_ORDENABLES, prefijoURL);
  const [filtrosLocales, setFiltrosLocales] = useState<FiltrosColumnasTareas>(porDefecto);
  const [ordenLocal, setOrdenLocal] = useState<OrdenTareas | null>(null);
  const enURL = prefijoURL !== undefined;
  const filtros = enURL ? filtrosURL : filtrosLocales;
  const orden = enURL ? ordenURL : ordenLocal;
  const setFiltros = enURL ? setFiltrosURL : setFiltrosLocales;
  const setOrden = enURL ? setOrdenURL : setOrdenLocal;

  // Con filtros locales, un nuevo preset recarga el filtro Estado ("ajustar estado durante el
  // render" de React, no un efecto). En la URL lo resuelve la página al cambiar el preset.
  const [presetPrevio, setPresetPrevio] = useState(preset);
  if (preset !== presetPrevio) {
    setPresetPrevio(preset);
    if (preset && !enURL) {
      setFiltrosLocales((actuales) => ({ ...actuales, estado: estadosDePreset(preset) }));
    }
  }

  const opciones = opcionesColumnasTareas(tareas);
  const filtradas = aplicarFiltrosColumnas(tareas, filtros);
  const visibles = ordenarTareas(filtradas, orden);

  if (telefono) {
    return (
      <ListaTareasMovil
        todas={tareas}
        tareas={visibles}
        proyectos={proyectos}
        filtros={filtros}
        vacios={porDefecto}
        conEstado={!preset}
        orden={orden}
        onFiltrosChange={setFiltros}
        onOrdenChange={setOrden}
      />
    );
  }

  return (
    <Table>
      <ContenedorTabla etiqueta="Tareas">
        <Table.Head>
          <Table.Row>
            {COLUMNAS.map((col) => (
              <EncabezadoTarea
                key={col.id}
                id={col.id}
                etiqueta={col.etiqueta}
                orden={orden}
                onOrden={(columna) => setOrden(siguienteOrden(orden, columna))}
                filtros={filtros}
                opciones={opciones}
                onFiltrosChange={(cambios) => setFiltros({ ...filtros, ...cambios })}
              />
            ))}
            {proyectos && <Table.HeaderCell>Proyecto</Table.HeaderCell>}
            <Table.HeaderCell>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {visibles.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={COLUMNAS.length + (proyectos ? 2 : 1)} className="empty">
                Sin tareas que cumplan los filtros.
              </Table.Cell>
            </Table.Row>
          ) : (
            visibles.map((tarea) => (
              <FilaTarea key={tarea.id} tarea={tarea} onVer={abrir} proyectos={proyectos} />
            ))
          )}
        </Table.Body>
      </ContenedorTabla>
    </Table>
  );
}

function FilaTarea({
  tarea,
  onVer,
  proyectos,
}: {
  tarea: Tarea;
  onVer: (ruta: string) => void;
  proyectos?: Proyecto[];
}) {
  const ruta = `/tareas/${tarea.id}`;
  // Solo amarillo/rojo resaltan la fila (verde es el caso normal, sin
  // urgencia — no necesita destacarse). Resuelta/Cancelada no muestran
  // semáforo en absoluto.
  const nivel = nivelAlertaSemaforo(tarea.estado, tarea.fechaInicio, tarea.fechaFin);
  const claseSemaforo = nivel ? `adoc-tt-row--${nivel}` : undefined;

  return (
    <Table.Row
      {...propsFilaClickeable(() => onVer(ruta))}
      className={`adoc-fila-clickeable adoc-tt-row--clicable${claseSemaforo ? ` ${claseSemaforo}` : ""}`}
    >
      <Table.Cell numeric className="adoc-mono">
        {tarea.numero}
      </Table.Cell>
      <Table.Cell>
        <EnlaceFila ruta={ruta} etiqueta={`Ver la tarea ${tarea.numero}: ${tarea.titulo}`}>
          {tarea.titulo}
        </EnlaceFila>
      </Table.Cell>
      <Table.Cell>{tarea.creadoPor.nombre}</Table.Cell>
      <Table.Cell>{tarea.responsable.nombre}</Table.Cell>
      <Table.Cell>{formatearFecha(tarea.fechaInicio)}</Table.Cell>
      <Table.Cell>{formatearFecha(tarea.fechaFin)}</Table.Cell>
      <Table.Cell>{ETIQUETA_PRIORIDAD[tarea.prioridad]}</Table.Cell>
      <Table.Cell numeric>{tarea.porcentajeAvance}%</Table.Cell>
      <Table.Cell>
        <EstadoTareaBadge estado={tarea.estado} />
      </Table.Cell>
      {proyectos && (
        <Table.Cell>{proyectos.find((p) => p.id === tarea.proyectoId)?.nombre ?? "—"}</Table.Cell>
      )}
      <Table.Cell className="adoc-table-actions">
        <Button
          variant="ghost"
          size="sm"
          onClick={(evento) => {
            evento.stopPropagation();
            onVer(ruta);
          }}
          aria-label={`Ver la tarea "${tarea.titulo}"`}
        >
          Ver
        </Button>
      </Table.Cell>
    </Table.Row>
  );
}

const COLUMNAS_ORDENABLES = COLUMNAS.map((columna) => columna.id);

function EncabezadoTarea({
  id,
  etiqueta,
  orden,
  onOrden,
  filtros,
  opciones,
  onFiltrosChange,
}: {
  id: ColumnaOrdenableTarea;
  etiqueta: string;
  orden: OrdenTareas | null;
  onOrden: (columna: ColumnaOrdenableTarea) => void;
  filtros: FiltrosColumnasTareas;
  opciones: ReturnType<typeof opcionesColumnasTareas>;
  onFiltrosChange: (cambios: Partial<FiltrosColumnasTareas>) => void;
}) {
  const valor = filtros[id];
  const esTexto = id === "numero" || id === "titulo" || id === "fechaInicio" || id === "fechaFin";
  const esNumero = id === "avance";
  const texto = typeof valor === "string" ? valor : "";
  const opcionesCampo =
    id === "autor"
      ? opciones.autores
      : id === "responsable"
        ? opciones.responsables
        : id === "prioridad"
          ? (["alta", "media", "baja"] satisfies Prioridad[])
          : id === "estado"
            ? (["pendiente", "en_curso", "pausa", "resuelta", "cancelada"] satisfies EstadoTarea[])
            : [];
  const etiquetas =
    id === "prioridad" ? ETIQUETA_PRIORIDAD : id === "estado" ? ETIQUETA_ESTADO : undefined;

  return (
    <Table.HeaderCell
      aria-label={etiqueta}
      sort={orden?.columna === id ? orden.direccion : null}
      onSortChange={() => onOrden(id)}
    >
      <span>
        {etiqueta}
        <FiltroEncabezado
          etiqueta={etiqueta}
          activo={
            esTexto || esNumero ? Boolean(texto.trim()) : Array.isArray(valor) && valor.length > 0
          }
          onLimpiar={() =>
            onFiltrosChange({
              [id]: esTexto || esNumero ? "" : [],
            } as Partial<FiltrosColumnasTareas>)
          }
        >
          {esTexto ? (
            <Input
              className="adoc-filtro-encabezado-campo"
              placeholder={`Buscar ${etiqueta.toLowerCase()}…`}
              aria-label={`Buscar ${etiqueta}`}
              value={texto}
              onChange={(evento) =>
                onFiltrosChange({ [id]: evento.target.value } as Partial<FiltrosColumnasTareas>)
              }
            />
          ) : esNumero ? (
            <Input
              type="number"
              min={0}
              max={100}
              className="adoc-filtro-encabezado-campo"
              placeholder="% exacto…"
              aria-label={`Buscar ${etiqueta}`}
              value={texto}
              onChange={(evento) =>
                onFiltrosChange({ [id]: evento.target.value } as Partial<FiltrosColumnasTareas>)
              }
            />
          ) : (
            <OpcionesFiltro
              opciones={opcionesCampo}
              valores={Array.isArray(valor) ? valor : []}
              onToggle={(opcion) =>
                onFiltrosChange({
                  [id]: alternarOpcion(Array.isArray(valor) ? valor : [], opcion),
                } as Partial<FiltrosColumnasTareas>)
              }
              etiquetas={etiquetas}
            />
          )}
        </FiltroEncabezado>
      </span>
    </Table.HeaderCell>
  );
}
