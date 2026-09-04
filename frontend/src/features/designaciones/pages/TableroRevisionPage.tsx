import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  FiltrosLista,
  type CampoFiltroFijo,
  type CampoFiltroOpcional,
} from "../../../shared/ui/FiltrosLista";
import { TablaRevision } from "../components/TablaRevision";
import { CARRERAS, FILTROS_INICIALES } from "../components/filtrosTablero";
import type { FiltrosTablero } from "../components/filtrosTablero";
import { useActorContexto } from "../hooks/useActorContexto";
import { useCatalogosDesignaciones } from "../hooks/useCatalogosDesignaciones";
import { usePedidosPorAmbito } from "../hooks/usePedidos";
import type { PedidoDesignacion } from "../types";

/**
 * Siempre visibles: lo que se busca primero es un docente o un tipo de novedad.
 * **Período** también es fijo, y no opcional, porque arranca aplicado (en el período
 * abierto): un filtro que acota desde el vamos no puede estar escondido detrás de
 * "+ Añadir filtro" — el usuario vería una lista recortada sin saber por qué.
 */
function filtrosFijos(
  periodos: { id: string; nombre: string; activo: boolean }[],
): CampoFiltroFijo[] {
  return [
    { clave: "nombre", placeholder: "Filtrar por docente…", ariaLabel: "Filtrar por docente" },
    {
      tipo: "select",
      clave: "tipo",
      ariaLabel: "Filtrar por tipo",
      opciones: [
        { value: "todos", label: "Tipo: Todos" },
        { value: "Alta", label: "Alta" },
        { value: "Baja", label: "Baja" },
        { value: "Cambio de cargo o dedicación", label: "Cambio" },
      ],
    },
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
    { tipo: "texto", clave: "legajo", etiqueta: "Legajo", placeholder: "Legajo…", ancho: "120px" },
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

  function handleSeleccionar(pedido: PedidoDesignacion) {
    navegar(`/designaciones/pedidos/${pedido.id}`);
  }

  const cantidad = pedidos?.length ?? 0;
  const ambito = actor.carrera ?? "Departamento";

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones", href: "/designaciones" },
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

      {!isLoading && !isError && cantidad > 0 && pedidos && (
        <TablaRevision
          pedidos={pedidos}
          actor={actor}
          filtros={filtros}
          onSeleccionar={handleSeleccionar}
        />
      )}
    </>
  );
}
