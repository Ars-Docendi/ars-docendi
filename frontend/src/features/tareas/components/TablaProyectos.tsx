import { Button, Input, Table } from "@ars-docendi/ui";
import { EnlaceFila } from "../../../shared/navegacion/EnlaceFila";
import { useFiltrosURL, useOrdenURL } from "../../../shared/navegacion/estadoURL";
import { useAbrirDetalle } from "../../../shared/navegacion/useEstadoVolver";
import { propsFilaClickeable } from "../../../shared/ui/filaClickeable";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import type { EstadoProyectoCatalogo, Proyecto } from "../types";
import { EstadoProyectoBadge } from "./EstadoProyectoBadge";
import { OpcionesFiltro } from "./OpcionesFiltro";
import { formatearFecha } from "./detalleAdapters";
import { alternarOpcion } from "./filtrosTareas";
import {
  aplicarFiltrosProyectos,
  FILTROS_PROYECTOS_INICIALES,
  responsablesDeProyectos,
} from "./filtrosProyectos";
import { ordenarProyectos, type ColumnaOrdenableProyecto } from "./ordenProyectos";
import { siguienteOrden } from "./ordenTareas";
import "./tablaTareas.css";

interface TablaProyectosProps {
  proyectos: Proyecto[];
  /** Catálogo de estados: define las opciones del filtro Estado y el orden por defecto. */
  estados: EstadoProyectoCatalogo[];
}

const COLUMNAS: { id: ColumnaOrdenableProyecto; etiqueta: string }[] = [
  { id: "numero", etiqueta: "N°" },
  { id: "nombre", etiqueta: "Nombre" },
  { id: "responsable", etiqueta: "Responsable" },
  { id: "fechaInicio", etiqueta: "Inicio" },
  { id: "fechaFin", etiqueta: "Fin" },
  { id: "estado", etiqueta: "Estado" },
];

const COLUMNAS_ORDENABLES = COLUMNAS.map((columna) => columna.id);

const COLUMNAS_DE_TEXTO: ColumnaOrdenableProyecto[] = [
  "numero",
  "nombre",
  "fechaInicio",
  "fechaFin",
];

/**
 * Listado de proyectos con el mismo modelo que `TablaTareas`: un filtro por columna en su
 * propio header (`FiltroEncabezado`) y orden por header con ciclo asc → desc → default. El
 * orden por defecto es por estado (según el catálogo) y luego por fecha de inicio.
 */
export function TablaProyectos({ proyectos, estados }: TablaProyectosProps) {
  const abrir = useAbrirDetalle();
  // Filtros y orden en el query: sobreviven a Atrás y a recargar.
  const [filtros, setFiltros] = useFiltrosURL(FILTROS_PROYECTOS_INICIALES);
  const [orden, setOrden] = useOrdenURL<ColumnaOrdenableProyecto>(COLUMNAS_ORDENABLES);

  const codigosEstado = estados.map((e) => e.codigo);
  const etiquetasEstado = Object.fromEntries(estados.map((e) => [e.codigo, e.nombre]));
  const responsables = responsablesDeProyectos(proyectos);
  const visibles = ordenarProyectos(
    aplicarFiltrosProyectos(proyectos, filtros),
    orden,
    codigosEstado,
  );

  return (
    <div className="adoc-tabla-scroll">
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              {COLUMNAS.map((col) => {
                const valor = filtros[col.id];
                const esTexto = COLUMNAS_DE_TEXTO.includes(col.id);
                const texto = typeof valor === "string" ? valor : "";
                const opciones = col.id === "responsable" ? responsables : codigosEstado;
                return (
                  <Table.HeaderCell
                    key={col.id}
                    aria-label={col.etiqueta}
                    sort={orden?.columna === col.id ? orden.direccion : null}
                    onSortChange={() => setOrden(siguienteOrden(orden, col.id))}
                  >
                    <span>
                      {col.etiqueta}
                      <FiltroEncabezado
                        etiqueta={col.etiqueta}
                        activo={
                          esTexto ? Boolean(texto.trim()) : Array.isArray(valor) && valor.length > 0
                        }
                        onLimpiar={() => setFiltros({ ...filtros, [col.id]: esTexto ? "" : [] })}
                      >
                        {esTexto ? (
                          <Input
                            className="adoc-filtro-encabezado-campo"
                            placeholder={`Buscar ${col.etiqueta.toLowerCase()}…`}
                            aria-label={`Buscar ${col.etiqueta}`}
                            value={texto}
                            onChange={(evento) =>
                              setFiltros({ ...filtros, [col.id]: evento.target.value })
                            }
                          />
                        ) : (
                          <OpcionesFiltro
                            opciones={opciones}
                            valores={Array.isArray(valor) ? valor : []}
                            etiquetas={col.id === "estado" ? etiquetasEstado : undefined}
                            onToggle={(opcion) =>
                              setFiltros({
                                ...filtros,
                                [col.id]: alternarOpcion(Array.isArray(valor) ? valor : [], opcion),
                              })
                            }
                          />
                        )}
                      </FiltroEncabezado>
                    </span>
                  </Table.HeaderCell>
                );
              })}
              <Table.HeaderCell>Acciones</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {visibles.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={COLUMNAS.length + 1} className="empty">
                  {proyectos.length === 0
                    ? "Todavía no hay proyectos cargados."
                    : "Sin proyectos que cumplan los filtros."}
                </Table.Cell>
              </Table.Row>
            ) : (
              visibles.map((proyecto) => (
                <Table.Row
                  key={proyecto.id}
                  {...propsFilaClickeable(() => abrir(`/tareas/proyectos/${proyecto.id}`))}
                  className="adoc-fila-clickeable adoc-tt-row--clicable"
                >
                  <Table.Cell numeric className="adoc-mono">
                    {proyecto.numero}
                  </Table.Cell>
                  <Table.Cell>
                    <EnlaceFila
                      ruta={`/tareas/proyectos/${proyecto.id}`}
                      etiqueta={`Ver el proyecto ${proyecto.numero}: ${proyecto.nombre}`}
                    >
                      {proyecto.nombre}
                    </EnlaceFila>
                  </Table.Cell>
                  <Table.Cell>{proyecto.responsable.nombre}</Table.Cell>
                  <Table.Cell>{formatearFecha(proyecto.fechaInicio)}</Table.Cell>
                  <Table.Cell>{formatearFecha(proyecto.fechaFin)}</Table.Cell>
                  <Table.Cell>
                    <EstadoProyectoBadge estado={proyecto.estado} nombre={proyecto.estadoNombre} />
                  </Table.Cell>
                  <Table.Cell className="adoc-table-actions">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={(evento) => {
                        evento.stopPropagation();
                        abrir(`/tareas/proyectos/${proyecto.id}`);
                      }}
                      aria-label={`Ver el proyecto "${proyecto.nombre}"`}
                    >
                      Ver
                    </Button>
                  </Table.Cell>
                </Table.Row>
              ))
            )}
          </Table.Body>
        </Table.Root>
      </Table>
    </div>
  );
}
