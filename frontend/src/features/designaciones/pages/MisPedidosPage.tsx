import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert, Modal, Select } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaMisPedidos } from "../components/TablaMisPedidos";
import { IconoArrowLeft, IconoArrowRight, IconoPlus, IconoSearch } from "../components/lucide";
import { PERIODOS_MOCK } from "../api/periodosMock";
import { useActorContexto } from "../hooks/useActorContexto";
import { useMisPedidos } from "../hooks/usePedidos";
import { useCancelarPedido, useEnviarPedido, useReenviarPedido } from "../hooks/useAccionesPedido";
import type { EstadoPedido, PedidoDesignacion } from "../types";
import "./misPedidos.css";

const PAGE_SIZE = 9;

type FiltroEstado =
  | "todos"
  | "borrador"
  | "revision"
  | "aprobado"
  | "rechazado"
  | "devuelto"
  | "cancelado";

function coincideEstado(estado: EstadoPedido, filtro: FiltroEstado): boolean {
  switch (filtro) {
    case "todos":
      return true;
    case "borrador":
      return estado === "borrador";
    case "revision":
      return estado.startsWith("en_revision");
    case "aprobado":
      return estado === "en_lote";
    case "rechazado":
      return estado === "rechazado";
    case "devuelto":
      return estado === "devuelto";
    case "cancelado":
      return estado === "cancelado";
  }
}

function coincideBusqueda(pedido: PedidoDesignacion, q: string): boolean {
  if (!q) return true;
  const aguja = q.toLowerCase();
  return (
    pedido.docente.nombre.toLowerCase().includes(aguja) ||
    (pedido.numero ?? "").toLowerCase().includes(aguja) ||
    pedido.asignaciones.some((a) => a.materia.toLowerCase().includes(aguja))
  );
}

export function MisPedidosPage() {
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { data: pedidos, isLoading, isError } = useMisPedidos(actor);
  const enviar = useEnviarPedido(actor);
  const cancelar = useCancelarPedido(actor);
  const reenviar = useReenviarPedido(actor);

  const [pedidoACancelar, setPedidoACancelar] = useState<PedidoDesignacion | undefined>();
  const [busqueda, setBusqueda] = useState("");
  const [filtro, setFiltro] = useState<FiltroEstado>("todos");
  const [pagina, setPagina] = useState(1);

  const periodo = PERIODOS_MOCK.find((p) => p.estado === "abierto");
  const total = pedidos?.length ?? 0;

  const filtrados = useMemo(
    () =>
      (pedidos ?? []).filter(
        (p) => coincideEstado(p.estado, filtro) && coincideBusqueda(p, busqueda),
      ),
    [pedidos, filtro, busqueda],
  );

  const totalFiltrado = filtrados.length;
  const totalPaginas = Math.max(1, Math.ceil(totalFiltrado / PAGE_SIZE));
  const paginaActual = Math.min(pagina, totalPaginas);
  const desde = (paginaActual - 1) * PAGE_SIZE;
  const visibles = filtrados.slice(desde, desde + PAGE_SIZE);

  function reiniciarPagina<T>(setter: (v: T) => void) {
    return (v: T) => {
      setter(v);
      setPagina(1);
    };
  }

  function handleConfirmarCancelar() {
    if (pedidoACancelar) cancelar.mutate(pedidoACancelar.id);
    setPedidoACancelar(undefined);
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones", href: "/designaciones" },
          { label: "Mis pedidos" },
        ]}
      />
      <PageHeader
        pretitle="Designaciones"
        title="Mis pedidos de designación"
        meta={
          isLoading
            ? "Cargando…"
            : `${total} pedido${total !== 1 ? "s" : ""}${
                periodo ? ` · Cuatrimestre ${periodo.anio} · ${periodo.cuatrimestre}` : ""
              }`
        }
        actions={
          <Button
            variant="primary"
            leadingIcon={<IconoPlus />}
            onClick={() => navegar("/designaciones/pedidos/nuevo")}
          >
            Nuevo pedido
          </Button>
        }
      />

      {(enviar.isError || cancelar.isError || reenviar.isError) && (
        <InlineAlert severity="danger" title="No se pudo completar la acción">
          Ocurrió un error al actualizar el pedido. Probá de nuevo.
        </InlineAlert>
      )}

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando tus pedidos…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los pedidos">
          Hubo un problema al obtener tus pedidos del período. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no cargaste pedidos">
          Empezá creando el primer pedido de designación del período con “Nuevo pedido”.
        </InlineAlert>
      )}

      {!isLoading && !isError && total > 0 && (
        <div className="adoc-mp-section">
          <div className="adoc-mp-toolbar">
            <div className="adoc-mp-search">
              <IconoSearch />
              <input
                type="search"
                value={busqueda}
                onChange={(e) => reiniciarPagina(setBusqueda)(e.target.value)}
                placeholder="Buscar por docente, N° o materia…"
                aria-label="Buscar pedidos"
              />
            </div>
            <span className="adoc-mp-estado">
              <Select
                aria-label="Filtrar por estado"
                value={filtro}
                onChange={(e) => reiniciarPagina(setFiltro)(e.target.value as FiltroEstado)}
              >
                <option value="todos">Todos los estados</option>
                <option value="borrador">Borrador</option>
                <option value="revision">En revisión</option>
                <option value="aprobado">Aprobado</option>
                <option value="devuelto">Devuelto</option>
                <option value="rechazado">Rechazado</option>
                <option value="cancelado">Cancelado</option>
              </Select>
            </span>
          </div>

          {totalFiltrado === 0 ? (
            <InlineAlert severity="info" title="Sin resultados">
              Ningún pedido coincide con la búsqueda o el filtro aplicado.
            </InlineAlert>
          ) : (
            <>
              <TablaMisPedidos
                pedidos={visibles}
                onVerDetalle={(p) => navegar(`/designaciones/pedidos/${p.id}`)}
                onEditar={(p) => navegar(`/designaciones/pedidos/${p.id}/editar`)}
                onEnviar={(p) => enviar.mutate(p.id)}
                onReenviar={(p) => reenviar.mutate(p.id)}
                onCancelar={(p) => setPedidoACancelar(p)}
              />

              <div className="adoc-mp-pager">
                <button
                  type="button"
                  className="adoc-mp-pager-btn"
                  disabled={paginaActual <= 1}
                  onClick={() => setPagina((p) => Math.max(1, p - 1))}
                >
                  <IconoArrowLeft /> Anterior
                </button>

                <div className="adoc-mp-pages">
                  {Array.from({ length: totalPaginas }, (_, i) => i + 1).map((n) => (
                    <button
                      key={n}
                      type="button"
                      className={`adoc-mp-page${n === paginaActual ? " activa" : ""}`}
                      aria-current={n === paginaActual ? "page" : undefined}
                      onClick={() => setPagina(n)}
                    >
                      {n}
                    </button>
                  ))}
                </div>

                <div className="adoc-mp-pager-right">
                  <button
                    type="button"
                    className="adoc-mp-pager-btn"
                    disabled={paginaActual >= totalPaginas}
                    onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))}
                  >
                    Siguiente <IconoArrowRight />
                  </button>
                  <span className="adoc-mp-pager-meta">
                    Registros {desde + 1}–{desde + visibles.length} de {totalFiltrado}
                  </span>
                </div>
              </div>
            </>
          )}
        </div>
      )}

      <Modal
        open={pedidoACancelar !== undefined}
        onOpenChange={(open) => {
          if (!open) setPedidoACancelar(undefined);
        }}
        title="Cancelar pedido"
        footer={
          <>
            <Button variant="secondary" onClick={() => setPedidoACancelar(undefined)}>
              Volver
            </Button>
            <Button variant="destructive" onClick={handleConfirmarCancelar}>
              Cancelar pedido
            </Button>
          </>
        }
      >
        <p style={{ margin: 0 }}>
          ¿Seguro que querés cancelar el pedido de{" "}
          <strong>{pedidoACancelar?.docente.nombre}</strong>? Esta acción es definitiva.
        </p>
      </Modal>
    </>
  );
}
