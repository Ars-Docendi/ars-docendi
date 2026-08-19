import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  FiltrosLista,
  type CampoFiltroFijo,
  type CampoFiltroOpcional,
} from "../../../shared/ui/FiltrosLista";
import { TablaMisPedidos } from "../components/TablaMisPedidos";
import { ModalEliminarPedido } from "../components/ModalEliminarPedido";
import { IconoArrowLeft, IconoArrowRight, IconoPlus } from "../components/lucide";
import {
  aplicarFiltrosMisPedidos,
  FILTROS_INICIALES,
  type FiltrosMisPedidosState,
} from "../components/filtrosMisPedidos";
import { useCatalogosDesignaciones } from "../hooks/useCatalogosDesignaciones";
import { useMisPedidos } from "../hooks/usePedidos";
import { useEliminarPedido } from "../hooks/useAccionesPedido";
import type { Novedad, PedidoDesignacion } from "../types";
import "./misPedidos.css";

const PAGE_SIZE = 9;

const NOVEDADES: Novedad[] = ["Sin novedad", "Alta", "Baja", "Cambio de cargo o dedicación"];

const FILTROS_FIJOS: CampoFiltroFijo[] = [
  { clave: "docente", placeholder: "Filtrar por docente…", ariaLabel: "Filtrar por docente" },
  {
    clave: "numero",
    placeholder: "Filtrar por N°…",
    ariaLabel: "Filtrar por N°",
    ancho: "chica",
  },
];

const FILTROS_OPCIONALES: CampoFiltroOpcional[] = [
  { tipo: "texto", clave: "legajo", etiqueta: "Legajo", placeholder: "Legajo…", ancho: "120px" },
  {
    tipo: "select",
    clave: "tipo",
    etiqueta: "Tipo",
    valorInicial: "todos",
    opciones: [
      { value: "todos", label: "Tipo: Todos" },
      ...NOVEDADES.map((n) => ({ value: n, label: n })),
    ],
  },
  {
    tipo: "select",
    clave: "estado",
    etiqueta: "Estado",
    valorInicial: "todos",
    opciones: [
      { value: "todos", label: "Todos los estados" },
      { value: "borrador", label: "Borrador" },
      { value: "revision", label: "En revisión" },
      { value: "aprobado", label: "Aprobado" },
      { value: "devuelto", label: "Devuelto" },
      { value: "rechazado", label: "Rechazado" },
      { value: "cancelado", label: "Cancelado" },
    ],
  },
];

export function MisPedidosPage() {
  const navegar = useNavigate();
  const { data: pedidos, isLoading, isError, refetch } = useMisPedidos();
  const catalogos = useCatalogosDesignaciones();
  const eliminar = useEliminarPedido();

  const [filtros, setFiltros] = useState<FiltrosMisPedidosState>(FILTROS_INICIALES);
  const [pagina, setPagina] = useState(1);
  const [pedidoAEliminar, setPedidoAEliminar] = useState<PedidoDesignacion | undefined>();

  function handleConfirmarEliminar() {
    if (!pedidoAEliminar) return;
    eliminar.mutate(pedidoAEliminar.id, {
      onSuccess: () => setPedidoAEliminar(undefined),
    });
  }

  const periodo = catalogos.data?.periodoActivo;
  const total = pedidos?.length ?? 0;

  function actualizarFiltros(nuevos: FiltrosMisPedidosState) {
    setFiltros(nuevos);
    setPagina(1);
  }

  const filtrados = useMemo(
    () => aplicarFiltrosMisPedidos(pedidos ?? [], filtros),
    [pedidos, filtros],
  );

  const totalFiltrado = filtrados.length;
  const totalPaginas = Math.max(1, Math.ceil(totalFiltrado / PAGE_SIZE));
  const paginaActual = Math.min(pagina, totalPaginas);
  const desde = (paginaActual - 1) * PAGE_SIZE;
  const visibles = filtrados.slice(desde, desde + PAGE_SIZE);

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
            : `${total} pedido${total !== 1 ? "s" : ""}${periodo ? ` · ${periodo.nombre}` : ""}`
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

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando tus pedidos…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los pedidos">
          Hubo un problema al obtener tus pedidos del período.{" "}
          <button onClick={() => refetch()}>Reintentar</button>.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no cargaste pedidos">
          Empezá creando el primer pedido de designación del período con “Nuevo pedido”.
        </InlineAlert>
      )}

      {!isLoading && !isError && total > 0 && (
        <div className="adoc-mp-section">
          <FiltrosLista
            fijos={FILTROS_FIJOS}
            opcionales={FILTROS_OPCIONALES}
            valores={filtros}
            onChange={actualizarFiltros}
          />

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
                onEliminar={(p) => setPedidoAEliminar(p)}
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

      <ModalEliminarPedido
        open={pedidoAEliminar !== undefined}
        onOpenChange={(open) => {
          if (!open) setPedidoAEliminar(undefined);
        }}
        pedido={pedidoAEliminar}
        error={eliminar.isError ? eliminar.error.message : undefined}
        eliminando={eliminar.isPending}
        onConfirmar={handleConfirmarEliminar}
      />
    </>
  );
}
