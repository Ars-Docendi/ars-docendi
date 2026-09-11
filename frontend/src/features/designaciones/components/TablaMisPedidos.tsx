import { Button, Input, Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";
import type { PedidoDesignacion } from "../types";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import {
  etiquetaEstadoFiltro,
  etiquetaNovedadCorta,
  fechaEnviado,
  opcionesEstadosMisPedidos,
  opcionesTiposMisPedidos,
  siguienteOrdenMisPedidos,
  type ColumnaOrdenMisPedidos,
  type FiltrosMisPedidosState,
  type OrdenMisPedidos,
} from "./filtrosMisPedidos";
import { EstadoPedidoPill } from "./EstadoPedidoPill";
import { IconoX } from "./lucide";

interface TablaMisPedidosProps {
  pedidos: PedidoDesignacion[];
  pedidosParaOpciones?: PedidoDesignacion[];
  filtros?: FiltrosMisPedidosState;
  orden?: OrdenMisPedidos | null;
  onFiltrosChange?: (filtros: FiltrosMisPedidosState) => void;
  onOrdenChange?: (orden: OrdenMisPedidos | null) => void;
  onVerDetalle: (pedido: PedidoDesignacion) => void;
  onEditar: (pedido: PedidoDesignacion) => void;
  onEliminar: (pedido: PedidoDesignacion) => void;
}

const SIN_CAMBIOS = () => {};

/** Tabla de Mis pedidos con filtros y orden controlados desde la página. */
export function TablaMisPedidos({
  pedidos,
  pedidosParaOpciones = pedidos,
  filtros = {
    docente: "",
    numero: "",
    legajo: "",
    catedra: "",
    enviado: "",
    tipo: [],
    estado: [],
  },
  orden = null,
  onFiltrosChange = SIN_CAMBIOS,
  onOrdenChange = SIN_CAMBIOS,
  onVerDetalle,
  onEditar,
  onEliminar,
}: TablaMisPedidosProps) {
  const tipos = opcionesTiposMisPedidos(pedidosParaOpciones);
  const estados = opcionesEstadosMisPedidos(pedidosParaOpciones);

  function cambiarFiltro<K extends keyof FiltrosMisPedidosState>(
    campo: K,
    valor: FiltrosMisPedidosState[K],
  ) {
    onFiltrosChange({ ...filtros, [campo]: valor });
  }

  function cambiarOrden(columna: ColumnaOrdenMisPedidos) {
    onOrdenChange(siguienteOrdenMisPedidos(orden, columna));
  }

  function alternarOpcion(campo: "tipo" | "estado", valor: string) {
    const valores = filtros[campo] as string[];
    cambiarFiltro(
      campo,
      (valores.includes(valor)
        ? valores.filter((actual) => actual !== valor)
        : [...valores, valor]) as FiltrosMisPedidosState[typeof campo],
    );
  }

  function limpiar(campo: keyof FiltrosMisPedidosState) {
    cambiarFiltro(campo, (Array.isArray(filtros[campo]) ? [] : "") as never);
  }

  return (
    <Table className="adoc-mp-table">
      <Table.Root aria-label="Mis pedidos de designación">
        <Table.Head>
          <Table.Row>
            <Encabezado
              etiqueta="N°"
              columna="numero"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar número…"
                  aria-label="Buscar N°"
                  value={filtros.numero}
                  onChange={(evento) => cambiarFiltro("numero", evento.target.value)}
                />
              }
              activo={Boolean(filtros.numero.trim())}
              onLimpiar={() => limpiar("numero")}
            />
            <Encabezado
              etiqueta="Docente"
              columna="docente"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar docente…"
                  aria-label="Buscar Docente"
                  value={filtros.docente}
                  onChange={(evento) => cambiarFiltro("docente", evento.target.value)}
                />
              }
              activo={Boolean(filtros.docente.trim())}
              onLimpiar={() => limpiar("docente")}
            />
            <Encabezado
              etiqueta="Legajo"
              columna="legajo"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar legajo…"
                  aria-label="Buscar Legajo"
                  value={filtros.legajo}
                  onChange={(evento) => cambiarFiltro("legajo", evento.target.value)}
                />
              }
              activo={Boolean(filtros.legajo.trim())}
              onLimpiar={() => limpiar("legajo")}
            />
            <Encabezado
              etiqueta="Cátedra"
              columna="catedra"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar cátedra…"
                  aria-label="Buscar Cátedra"
                  value={filtros.catedra}
                  onChange={(evento) => cambiarFiltro("catedra", evento.target.value)}
                />
              }
              activo={Boolean(filtros.catedra.trim())}
              onLimpiar={() => limpiar("catedra")}
            />
            <Encabezado
              etiqueta="Tipo"
              columna="tipo"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Opciones
                  opciones={tipos}
                  valores={filtros.tipo}
                  onToggle={(valor) => alternarOpcion("tipo", valor)}
                  etiquetas={{ "Cambio de cargo o dedicación": "Cambio" }}
                />
              }
              activo={filtros.tipo.length > 0}
              onLimpiar={() => limpiar("tipo")}
            />
            <Encabezado
              etiqueta="Enviado"
              columna="enviado"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Input
                  className="adoc-filtro-encabezado-campo"
                  placeholder="Buscar fecha…"
                  aria-label="Buscar Enviado"
                  value={filtros.enviado}
                  onChange={(evento) => cambiarFiltro("enviado", evento.target.value)}
                />
              }
              activo={Boolean(filtros.enviado.trim())}
              onLimpiar={() => limpiar("enviado")}
            />
            <Encabezado
              etiqueta="Estado"
              columna="estado"
              orden={orden}
              onOrden={cambiarOrden}
              filtro={
                <Opciones
                  opciones={estados}
                  valores={filtros.estado}
                  onToggle={(valor) => alternarOpcion("estado", valor)}
                  etiquetas={Object.fromEntries(
                    estados.map((estado) => [estado, etiquetaEstadoFiltro(estado)]),
                  )}
                />
              }
              activo={filtros.estado.length > 0}
              onLimpiar={() => limpiar("estado")}
            />
            <Table.HeaderCell>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {pedidos.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={8} className="empty">
                Sin resultados para los filtros aplicados.
              </Table.Cell>
            </Table.Row>
          ) : (
            pedidos.map((pedido) => (
              <Table.Row
                className="adoc-mp-row adoc-mp-row--clickeable"
                key={pedido.id}
                tabIndex={0}
                aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
                onClick={() => onVerDetalle(pedido)}
                onKeyDown={(evento) => {
                  if (evento.key === "Enter" || evento.key === " ") {
                    evento.preventDefault();
                    onVerDetalle(pedido);
                  }
                }}
              >
                <Table.Cell className="adoc-mp-num">{pedido.numero ?? "—"}</Table.Cell>
                <Table.Cell className="adoc-mp-doc">{pedido.docente.nombre}</Table.Cell>
                <Table.Cell className="adoc-mp-leg">{pedido.docente.legajo ?? "—"}</Table.Cell>
                <Table.Cell className="adoc-mp-cat">{pedido.catedra}</Table.Cell>
                <Table.Cell className="adoc-mp-nov">{etiquetaNovedadCorta(pedido)}</Table.Cell>
                <Table.Cell className="adoc-mp-env">{fechaEnviado(pedido)}</Table.Cell>
                <Table.Cell className="adoc-mp-est">
                  <EstadoPedidoPill estado={pedido.estado} />
                </Table.Cell>
                <Table.Cell className="adoc-mp-acc">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={(evento) => {
                      evento.stopPropagation();
                      onVerDetalle(pedido);
                    }}
                  >
                    Ver
                  </Button>
                  {pedido.accionesPermitidas?.includes("editar") && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={(evento) => {
                        evento.stopPropagation();
                        onEditar(pedido);
                      }}
                    >
                      Editar
                    </Button>
                  )}
                  {pedido.accionesPermitidas?.includes("eliminar") && (
                    <button
                      type="button"
                      className="adoc-mp-eliminar"
                      aria-label={`Eliminar pedido de ${pedido.docente.nombre}`}
                      onClick={(evento) => {
                        evento.stopPropagation();
                        onEliminar(pedido);
                      }}
                    >
                      <IconoX />
                    </button>
                  )}
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table.Root>
    </Table>
  );
}

function Encabezado({
  etiqueta,
  columna,
  orden,
  onOrden,
  filtro,
  activo,
  onLimpiar,
}: {
  etiqueta: string;
  columna: ColumnaOrdenMisPedidos;
  orden: OrdenMisPedidos | null;
  onOrden: (columna: ColumnaOrdenMisPedidos) => void;
  filtro: ReactNode;
  activo: boolean;
  onLimpiar: () => void;
}) {
  return (
    <Table.HeaderCell
      aria-label={etiqueta}
      sort={orden?.columna === columna ? orden.direccion : null}
      onSortChange={() => onOrden(columna)}
    >
      <span>
        {etiqueta}
        <FiltroEncabezado etiqueta={etiqueta} activo={activo} onLimpiar={onLimpiar}>
          {filtro}
        </FiltroEncabezado>
      </span>
    </Table.HeaderCell>
  );
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
