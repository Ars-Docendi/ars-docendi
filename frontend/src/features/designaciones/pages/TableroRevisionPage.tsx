import axios from "axios";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  FiltrosLista,
  type CampoFiltroFijo,
  type CampoFiltroOpcional,
} from "../../../shared/ui/FiltrosLista";
import { exportarLote } from "../api/loteApi";
import { TablaRevision } from "../components/TablaRevision";
import { CARRERAS, FILTROS_INICIALES } from "../components/filtrosTablero";
import type { FiltrosTablero } from "../components/filtrosTablero";
import { useActorContexto } from "../hooks/useActorContexto";
import { useCatalogosDesignaciones } from "../hooks/useCatalogosDesignaciones";
import { usePedidosPorAmbito } from "../hooks/usePedidos";
import type { PedidoDesignacion } from "../types";

/**
 * Período es el único filtro general fijo. Docente, Tipo, Legajo y Estado viven
 * en los encabezados de la tabla; así no aparecen dos controles para lo mismo.
 */
function filtrosFijos(
  periodos: { id: string; nombre: string; activo: boolean }[],
): CampoFiltroFijo[] {
  return [
    {
      tipo: "select",
      clave: "periodo",
      ariaLabel: "Filtrar por período",
      opciones: [
        ...periodos.map((periodo) => ({
          value: periodo.id,
          label: periodo.activo ? `${periodo.nombre} (abierto)` : periodo.nombre,
        })),
        { value: "todos", label: "Todos los períodos" },
      ],
    },
  ];
}

/**
 * Filtros opcionales, detrás de "+ Añadir filtro". El filtro **Carrera** solo se
 * ofrece a quien ve más de una carrera: para un Coordinador, cuyo ámbito ES una
 * carrera [BR-designaciones-009], no acotaría nada.
 */
function filtrosOpcionales(veVariasCarreras: boolean): CampoFiltroOpcional[] {
  return [
    {
      tipo: "select",
      clave: "prioridad",
      etiqueta: "Prioridad",
      valorInicial: "todos",
      opciones: [
        { value: "todos", label: "Prioridad: Todas" },
        { value: "prioritarios", label: "Solo prioritarios" },
        { value: "normales", label: "Sin prioridad" },
      ],
    },
    {
      tipo: "select",
      clave: "sinMovimiento",
      etiqueta: "Sin movimiento",
      valorInicial: "todos",
      opciones: [
        { value: "todos", label: "Movimiento: Todos" },
        { value: "7", label: "Sin mover +7 días" },
        { value: "15", label: "Sin mover +15 días" },
        { value: "30", label: "Sin mover +30 días" },
      ],
    },
    ...(veVariasCarreras
      ? [
          {
            tipo: "select" as const,
            clave: "carrera",
            etiqueta: "Carrera",
            valorInicial: "todos",
            opciones: [
              { value: "todos", label: "Carrera: Todas" },
              ...CARRERAS.map((carrera) => ({ value: carrera, label: carrera })),
            ],
          },
        ]
      : []),
  ];
}

export function TableroRevisionPage() {
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { data: pedidos, isLoading, isError, refetch } = usePedidosPorAmbito();
  const catalogos = useCatalogosDesignaciones();
  const [filtros, setFiltros] = useState<FiltrosTablero>(FILTROS_INICIALES);
  const [exportando, setExportando] = useState(false);
  const [errorExportacion, setErrorExportacion] = useState<string>();

  function handleSeleccionar(pedido: PedidoDesignacion) {
    navegar(`/designaciones/pedidos/${pedido.id}`);
  }

  async function handleExportar() {
    const periodo = catalogos.data?.periodoActivo;
    if (!periodo || exportando) return;
    setExportando(true);
    setErrorExportacion(undefined);
    try {
      const archivo = await exportarLote(periodo.id);
      const url = URL.createObjectURL(archivo);
      const enlace = document.createElement("a");
      enlace.href = url;
      enlace.download = "lote-designaciones.xlsx";
      enlace.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 409) {
        setErrorExportacion("El período cambió. Actualizamos los períodos; reintentá la descarga.");
        await catalogos.refetch();
      } else {
        setErrorExportacion(mensajeProblema(error, "No se pudo descargar el lote. Reintentá."));
      }
    } finally {
      setExportando(false);
    }
  }

  const cantidad = pedidos?.length ?? 0;
  const ambito = actor.carrera ?? "Departamento";
  const puedeExportarLote =
    actor.rol === "Secretaría" || actor.rol === "Decanato" || actor.rol === "Administración";

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones" },
          { label: "Tablero de revisión" },
        ]}
      />
      <PageHeader
        pretitle="Designaciones"
        title="Tablero de revisión de pedidos"
        meta={`Pedidos en tu ámbito · ${actor.rol} · ${ambito}`}
      />

      {isLoading && (
        <p style={{ color: "var(--color-text-secondary)" }}>Cargando los pedidos de tu ámbito…</p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los pedidos">
          Hubo un problema al obtener los pedidos de revisión.{" "}
          <button onClick={() => refetch()}>Reintentar</button>.
        </InlineAlert>
      )}

      {!isLoading && !isError && cantidad === 0 && (
        <InlineAlert severity="info" title="No hay pedidos para revisar">
          Cuando tu ámbito tenga pedidos en revisión, vas a verlos acá organizados por etapa.
        </InlineAlert>
      )}

      {!isLoading && !isError && cantidad > 0 && (
        <FiltrosLista
          fijos={filtrosFijos(catalogos.data?.periodos ?? [])}
          opcionales={filtrosOpcionales(actor.carrera === undefined)}
          valores={filtros}
          onChange={setFiltros}
        />
      )}

      {!isLoading && !isError && (cantidad > 0 || puedeExportarLote) && pedidos && (
        <TablaRevision
          pedidos={pedidos}
          actor={actor}
          filtros={filtros}
          onSeleccionar={handleSeleccionar}
          periodoActivo={catalogos.data?.periodoActivo}
          onExportar={handleExportar}
          exportando={exportando}
          errorExportacion={errorExportacion}
        />
      )}
    </>
  );
}
