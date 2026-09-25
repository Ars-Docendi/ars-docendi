import { useState } from "react";
import { Button, Input, Table, Tabs } from "@ars-docendi/ui";
import type { ActorContexto, PedidoDesignacion, PeriodoDesignacion } from "../types";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import {
  PESTANIAS,
  areaEsFiltrable,
  areaActual,
  etiquetaEstado,
  inicioEnCircuito,
  ordenarPedidos,
  pedidosDePestania,
  pestaniaInicial,
  siguienteOrden,
  ultimaActualizacion,
  type ColumnaOrdenable,
  type IdPestania,
  type OrdenTabla,
} from "./tableroRevisionModelo";
import {
  aplicarFiltros,
  FILTROS_COLUMNAS_INICIALES,
  opcionesColumnasTablero,
  type FiltrosColumnasTablero,
  type FiltrosTablero,
} from "./filtrosTablero";
import { NovedadChip } from "./NovedadChip";
import { EstadoPedidoBadge } from "./EstadoPedidoBadge";
import "./revision.css";
import { propsFilaClickeable } from "../../../shared/ui/filaClickeable";
import { TextoRecortado } from "../../../shared/ui/TextoRecortado";

interface TablaRevisionProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  filtros: FiltrosTablero;
  filtrosColumnas?: FiltrosColumnasTablero;
  onFiltrosColumnasChange?: (filtros: FiltrosColumnasTablero) => void;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
  periodoActivo?: PeriodoDesignacion | null;
  onExportar?: () => void;
  exportando?: boolean;
  errorExportacion?: string;
}

/** Alto de la grilla: la página no scrollea, scrollea la tabla con el encabezado fijo. */
const ALTO_TABLA = "calc(100vh - 452px)";

/** Ancho mínimo de Docente: usa el espacio que haya y recorta con "…" solo si no alcanza. */
const ANCHO_DOCENTE = 180;
const ANCHO_CHIP_PRIORITARIO = 88;

/** Columnas ordenables y su rótulo. "Área" queda fuera a propósito. */
const COLUMNAS: { id: ColumnaOrdenable; etiqueta: string }[] = [
  { id: "docente", etiqueta: "Docente" },
  { id: "legajo", etiqueta: "Legajo" },
  { id: "tipo", etiqueta: "Tipo" },
  { id: "inicio", etiqueta: "Inicio" },
  { id: "ultima", etiqueta: "Últ. actualización" },
  { id: "estado", etiqueta: "Estado" },
];

/**
 * Tabla de revisión: una sola tabla (`Table` del design system, igual que
 * Usuarios/Docentes/Períodos) con pestañas por área del circuito arriba y los
 * filtros generales siguen arriba de las pestañas; los de columnas viven en
 * cada encabezado y se aplican antes de los contadores.
 */
export function TablaRevision({
  pedidos,
  actor,
  filtros,
  filtrosColumnas,
  onFiltrosColumnasChange,
  onSeleccionar,
  periodoActivo,
  onExportar,
  exportando = false,
  errorExportacion,
}: TablaRevisionProps) {
  const [pestania, setPestania] = useState<IdPestania>(() => pestaniaInicial(actor));
  const [orden, setOrden] = useState<OrdenTabla | null>(null);
  const [filtrosColumnasLocales, setFiltrosColumnasLocales] = useState<FiltrosColumnasTablero>(
    FILTROS_COLUMNAS_INICIALES,
  );
  const filtrosColumnasActuales = filtrosColumnas ?? filtrosColumnasLocales;
  const opciones = opcionesColumnasTablero(pedidos);
  const filtrosColumnasAplicados = areaEsFiltrable(pestania)
    ? filtrosColumnasActuales
    : { ...filtrosColumnasActuales, area: [] };

  const filtrados = aplicarFiltros(pedidos, filtros, filtrosColumnasAplicados);
  const items = PESTANIAS.map(({ id, etiqueta }) => ({
    id,
    label: etiqueta,
    count: pedidosDePestania(filtrados, id).length,
  }));
  const visibles = ordenarPedidos(pedidosDePestania(filtrados, pestania), orden);
  // El área solo aporta en "Todos": en una pestaña de área es constante en todas las
  // filas y ya la dice la pestaña. En Finalizados no hay área que mostrar.
  const mostrarArea = areaEsFiltrable(pestania);
  const columnasVisibles = COLUMNAS.length + (mostrarArea ? 1 : 0);
  const puedeExportar =
    actor.rol === "Secretaría" || actor.rol === "Decanato" || actor.rol === "Administración";
  const mostrarExportar = pestania === "finalizados" && puedeExportar && Boolean(onExportar);

  return (
    <div className="adoc-revision">
      <div className="adoc-revision-tabs-row">
        <Tabs
          items={items}
          value={pestania}
          onChange={(id) => setPestania(id as IdPestania)}
          aria-label="Área del circuito"
        />
        {mostrarExportar && (
          <div className="adoc-revision-exportar">
            <span className="adoc-revision-periodo">
              {periodoActivo ? `Período: ${periodoActivo.nombre}` : "Sin período activo"}
            </span>
            <Button
              variant="secondary"
              size="sm"
              disabled={!periodoActivo || exportando}
              loading={exportando}
              onClick={onExportar}
              aria-describedby={!periodoActivo ? "revision-sin-periodo" : undefined}
            >
              Exportar
            </Button>
            {!periodoActivo && (
              <span id="revision-sin-periodo" className="adoc-revision-exportar-ayuda">
                Configurá un período activo para descargar el lote.
              </span>
            )}
            {exportando && <span role="status">Exportando…</span>}
          </div>
        )}
      </div>
      {errorExportacion && (
        <p className="adoc-revision-exportar-error" role="alert">
          {errorExportacion}
        </p>
      )}

      <Table>
        <Table.Root maxHeight={ALTO_TABLA}>
          <Table.Head>
            <Table.Row>
              {COLUMNAS.map(({ id, etiqueta }) => (
                <EncabezadoRevision
                  key={id}
                  id={id}
                  etiqueta={etiqueta}
                  orden={orden}
                  onOrden={(columna) => setOrden((previo) => siguienteOrden(previo, columna))}
                  filtros={filtrosColumnasActuales}
                  opciones={opciones}
                  onFiltrosChange={(cambios) => {
                    const nuevos = { ...filtrosColumnasActuales, ...cambios };
                    if (!filtrosColumnas) setFiltrosColumnasLocales(nuevos);
                    onFiltrosColumnasChange?.(nuevos);
                  }}
                />
              ))}
              {mostrarArea && (
                <Table.HeaderCell>
                  <span>
                    Área
                    <FiltroEncabezado
                      etiqueta="Área"
                      activo={filtrosColumnasActuales.area.length > 0}
                      onLimpiar={() => {
                        const nuevos = { ...filtrosColumnasActuales, area: [] };
                        if (!filtrosColumnas) setFiltrosColumnasLocales(nuevos);
                        onFiltrosColumnasChange?.(nuevos);
                      }}
                    >
                      <Opciones
                        opciones={opciones.areas}
                        valores={filtrosColumnasActuales.area}
                        onToggle={(valor) => {
                          const valores = alternar(filtrosColumnasActuales.area, valor);
                          const nuevos = { ...filtrosColumnasActuales, area: valores };
                          if (!filtrosColumnas) setFiltrosColumnasLocales(nuevos);
                          onFiltrosColumnasChange?.(nuevos);
                        }}
                      />
                    </FiltroEncabezado>
                  </span>
                </Table.HeaderCell>
              )}
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {visibles.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={columnasVisibles} className="empty">
                  Sin pedidos que cumplan los filtros.
                </Table.Cell>
              </Table.Row>
            ) : (
              visibles.map((pedido) => (
                <FilaPedido
                  key={pedido.id}
                  pedido={pedido}
                  mostrarArea={mostrarArea}
                  onVer={onSeleccionar}
                />
              ))
            )}
          </Table.Body>
        </Table.Root>
      </Table>
    </div>
  );
}

function FilaPedido({
  pedido,
  mostrarArea,
  onVer,
}: {
  pedido: PedidoDesignacion;
  mostrarArea: boolean;
  onVer: (pedido: PedidoDesignacion) => void;
}) {
  return (
    <Table.Row
      {...propsFilaClickeable(() => onVer(pedido))}
      aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
    >
      <Table.Cell>
        <span className="adoc-tabla-docente">
          <span className="adoc-tabla-nombre">
            <TextoRecortado
              texto={pedido.docente.nombre}
              // Con el chip, el nombre cede su lugar: la columna no se ensancha.
              anchoMinimo={
                pedido.prioritario ? ANCHO_DOCENTE - ANCHO_CHIP_PRIORITARIO : ANCHO_DOCENTE
              }
            />
          </span>
          {pedido.prioritario && <span className="adoc-chip-prioritario">Prioritario</span>}
        </span>
      </Table.Cell>
      <Table.Cell className="adoc-mono">{pedido.docente.legajo ?? "—"}</Table.Cell>
      <Table.Cell>
        <NovedadChip novedad={pedido.novedad} />
      </Table.Cell>
      <Table.Cell className="adoc-sin-salto">{inicioEnCircuito(pedido) ?? "—"}</Table.Cell>
      <Table.Cell className="adoc-sin-salto">{ultimaActualizacion(pedido) ?? "—"}</Table.Cell>
      <Table.Cell>
        <EstadoPedidoBadge estado={pedido.estado} etiqueta={etiquetaEstado(pedido)} />
      </Table.Cell>
      {mostrarArea && <Table.Cell>{areaActual(pedido) ?? "—"}</Table.Cell>}
    </Table.Row>
  );
}

function EncabezadoRevision({
  id,
  etiqueta,
  orden,
  onOrden,
  filtros,
  opciones,
  onFiltrosChange,
}: {
  id: ColumnaOrdenable;
  etiqueta: string;
  orden: OrdenTabla | null;
  onOrden: (columna: ColumnaOrdenable) => void;
  filtros: FiltrosColumnasTablero;
  opciones: ReturnType<typeof opcionesColumnasTablero>;
  onFiltrosChange: (cambios: Partial<FiltrosColumnasTablero>) => void;
}) {
  const campo = id;
  const valor = filtros[campo];
  const texto = typeof valor === "string" ? valor : "";
  const esTexto = id === "docente" || id === "legajo" || id === "inicio" || id === "ultima";
  const opcionesCampo = id === "tipo" ? opciones.tipos : id === "estado" ? opciones.estados : [];

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
          activo={esTexto ? Boolean(texto.trim()) : Array.isArray(valor) && valor.length > 0}
          onLimpiar={() =>
            onFiltrosChange({ [campo]: esTexto ? "" : [] } as Partial<FiltrosColumnasTablero>)
          }
        >
          {esTexto ? (
            <Input
              className="adoc-filtro-encabezado-campo"
              placeholder={`Buscar ${etiqueta.toLowerCase()}…`}
              aria-label={`Buscar ${etiqueta}`}
              value={texto}
              onChange={(evento) =>
                onFiltrosChange({ [campo]: evento.target.value } as Partial<FiltrosColumnasTablero>)
              }
            />
          ) : (
            <Opciones
              opciones={opcionesCampo}
              valores={Array.isArray(valor) ? valor : []}
              onToggle={(opcion) =>
                onFiltrosChange({
                  [campo]: alternar(Array.isArray(valor) ? valor : [], opcion),
                } as Partial<FiltrosColumnasTablero>)
              }
              etiquetas={ETIQUETAS_ESTADO_REVISION}
            />
          )}
        </FiltroEncabezado>
      </span>
    </Table.HeaderCell>
  );
}

const ETIQUETAS_ESTADO_REVISION: Record<string, string> = {
  en_revision_coordinador: "En revisión · Coordinador",
  en_revision_secretaria: "En revisión · Secretaría",
  en_revision_decanato: "En revisión · Decanato",
  devuelto: "Devuelto",
  en_lote: "En lote",
  rechazado: "Rechazado",
  cancelado: "Cancelado",
};

function alternar(valores: string[], valor: string): string[] {
  return valores.includes(valor)
    ? valores.filter((actual) => actual !== valor)
    : [...valores, valor];
}

function Opciones({
  opciones,
  valores,
  onToggle,
  etiquetas = {},
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
          {etiquetas[opcion] ?? opcion}
        </label>
      ))}
    </div>
  );
}
