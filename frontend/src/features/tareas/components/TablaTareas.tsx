import { useState } from "react";
import { Button, Input, Table } from "@ars-docendi/ui";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import type { EstadoTarea, Prioridad, Tarea } from "../types";
import { EstadoTareaBadge } from "./EstadoTareaBadge";
import { estadoSemaforo, muestraSemaforo } from "./semaforoTarea";
import { formatearFecha } from "./detalleAdapters";
import {
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
import "./tablaTareas.css";

interface TablaTareasProps {
  tareas: Tarea[];
  onSeleccionar: (tarea: Tarea) => void;
}

const ETIQUETA_PRIORIDAD: Record<Prioridad, string> = {
  alta: "Alta",
  media: "Media",
  baja: "Baja",
};

const ETIQUETA_ESTADO: Record<EstadoTarea, string> = {
  pendiente: "Pendiente",
  en_curso: "En curso",
  pausa: "Pausa",
  resuelta: "Resuelta",
  cancelada: "Cancelada",
};

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
export function TablaTareas({ tareas, onSeleccionar }: TablaTareasProps) {
  const [orden, setOrden] = useState<OrdenTareas | null>(null);
  const [filtros, setFiltros] = useState<FiltrosColumnasTareas>(FILTROS_COLUMNAS_INICIALES);

  const opciones = opcionesColumnasTareas(tareas);
  const filtradas = aplicarFiltrosColumnas(tareas, filtros);
  const visibles = ordenarTareas(filtradas, orden);

  return (
    <div className="adoc-tabla-scroll">
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              {COLUMNAS.map((col) => (
                <EncabezadoTarea
                  key={col.id}
                  id={col.id}
                  etiqueta={col.etiqueta}
                  orden={orden}
                  onOrden={(columna) => setOrden((actual) => siguienteOrden(actual, columna))}
                  filtros={filtros}
                  opciones={opciones}
                  onFiltrosChange={(cambios) =>
                    setFiltros((actuales) => ({ ...actuales, ...cambios }))
                  }
                />
              ))}
              <Table.HeaderCell>Acciones</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {visibles.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={COLUMNAS.length + 1} className="empty">
                  Sin tareas que cumplan los filtros.
                </Table.Cell>
              </Table.Row>
            ) : (
              visibles.map((tarea) => (
                <FilaTarea key={tarea.id} tarea={tarea} onVer={onSeleccionar} />
              ))
            )}
          </Table.Body>
        </Table.Root>
      </Table>
    </div>
  );
}

function FilaTarea({ tarea, onVer }: { tarea: Tarea; onVer: (tarea: Tarea) => void }) {
  // Solo amarillo/rojo resaltan la fila (verde es el caso normal, sin
  // urgencia — no necesita destacarse). Resuelta/Cancelada no muestran
  // semáforo en absoluto (`muestraSemaforo`).
  const semaforo = muestraSemaforo(tarea.estado)
    ? estadoSemaforo(tarea.fechaInicio, tarea.fechaFin)
    : null;
  const claseSemaforo =
    semaforo === "red"
      ? "adoc-tt-row--vencida"
      : semaforo === "yellow"
        ? "adoc-tt-row--por-vencer"
        : undefined;

  return (
    <Table.Row
      className={`adoc-tt-row--clicable${claseSemaforo ? ` ${claseSemaforo}` : ""}`}
      onClick={() => onVer(tarea)}
    >
      <Table.Cell numeric className="adoc-mono">
        {tarea.numero}
      </Table.Cell>
      <Table.Cell>{tarea.titulo}</Table.Cell>
      <Table.Cell>{tarea.creadoPor.nombre}</Table.Cell>
      <Table.Cell>{tarea.responsable.nombre}</Table.Cell>
      <Table.Cell>{formatearFecha(tarea.fechaInicio)}</Table.Cell>
      <Table.Cell>{formatearFecha(tarea.fechaFin)}</Table.Cell>
      <Table.Cell>{ETIQUETA_PRIORIDAD[tarea.prioridad]}</Table.Cell>
      <Table.Cell numeric>{tarea.porcentajeAvance}%</Table.Cell>
      <Table.Cell>
        <EstadoTareaBadge estado={tarea.estado} />
      </Table.Cell>
      <Table.Cell className="adoc-table-actions">
        <Button
          variant="ghost"
          size="sm"
          onClick={(evento) => {
            evento.stopPropagation();
            onVer(tarea);
          }}
          aria-label={`Ver la tarea "${tarea.titulo}"`}
        >
          Ver
        </Button>
      </Table.Cell>
    </Table.Row>
  );
}

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
            <Opciones
              opciones={opcionesCampo}
              valores={Array.isArray(valor) ? valor : []}
              onToggle={(opcion) =>
                onFiltrosChange({
                  [id]: alternar(Array.isArray(valor) ? valor : [], opcion),
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

function alternar(valores: string[], valor: string): string[] {
  return valores.includes(valor)
    ? valores.filter((actual) => actual !== valor)
    : [...valores, valor];
}

function Opciones({
  opciones,
  valores,
  onToggle,
  etiquetas,
}: {
  opciones: string[];
  valores: string[];
  onToggle: (valor: string) => void;
  etiquetas?: Record<string, string>;
}) {
  return (
    <div className="adoc-filtro-encabezado-opciones">
      {opciones.map((opcion) => (
        <label className="adoc-filtro-encabezado-opcion" key={opcion}>
          <input
            type="checkbox"
            checked={valores.includes(opcion)}
            onChange={() => onToggle(opcion)}
          />
          {etiquetas?.[opcion] ?? opcion}
        </label>
      ))}
    </div>
  );
}
