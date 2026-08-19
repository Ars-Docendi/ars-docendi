import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, InlineAlert, Select } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  FiltrosLista,
  type CampoFiltroFijo,
  type CampoFiltroOpcional,
} from "../../../shared/ui/FiltrosLista";
import { TablaRevision } from "../components/TablaRevision";
import { CARRERAS, FILTROS_INICIALES } from "../components/filtrosTablero";
import type { FiltrosTablero, VistaTablero } from "../components/filtrosTablero";
import { useActorContexto } from "../hooks/useActorContexto";
import { usePedidosPorAmbito } from "../hooks/usePedidos";
import type { PedidoDesignacion } from "../types";

const FILTROS_FIJOS: CampoFiltroFijo[] = [
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
];

const FILTROS_OPCIONALES: CampoFiltroOpcional[] = [
  { tipo: "texto", clave: "legajo", etiqueta: "Legajo", placeholder: "Legajo…", ancho: "120px" },
  {
    tipo: "select",
    clave: "prioridad",
    etiqueta: "Prioridad",
    valorInicial: "todos",
    opciones: [
      { value: "todos", label: "Prioritario: Todos" },
      { value: "prioritarios", label: "Solo prioritarios" },
      { value: "normales", label: "Sin prioridad" },
    ],
  },
  {
    tipo: "select",
    clave: "carrera",
    etiqueta: "Carrera",
    valorInicial: "todos",
    opciones: [
      { value: "todos", label: "Carrera: Todas" },
      ...CARRERAS.map((carrera) => ({ value: carrera, label: carrera })),
    ],
  },
];

export function TableroRevisionPage() {
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { data: pedidos, isLoading, isError } = usePedidosPorAmbito(actor);
  const [filtros, setFiltros] = useState<FiltrosTablero>(FILTROS_INICIALES);

  function handleSeleccionar(pedido: PedidoDesignacion) {
    navegar(`/designaciones/pedidos/${pedido.id}`);
  }

  const cantidad = pedidos?.length ?? 0;
  const ambito = actor.carrera ?? "Departamento";

  const vistaUI = (
    <div className="adoc-tablero-filtros">
      <Select
        aria-label="Filtrar pedidos por turno"
        wrapClassName="adoc-filtro-activo"
        value={filtros.vista}
        onChange={(e) => setFiltros((f) => ({ ...f, vista: e.target.value as VistaTablero }))}
      >
        <option value="completa">Vista completa</option>
        <option value="mis-pendientes">Mis pendientes</option>
      </Select>
    </div>
  );

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
        actions={!isLoading && !isError && cantidad > 0 ? vistaUI : undefined}
      />

      {!isLoading && !isError && cantidad > 0 && (
        <FiltrosLista
          fijos={FILTROS_FIJOS}
          opcionales={FILTROS_OPCIONALES}
          valores={filtros}
          onChange={setFiltros}
        />
      )}

      {isLoading && (
        <p style={{ color: "var(--color-text-secondary)" }}>Cargando los pedidos de tu ámbito…</p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los pedidos">
          Hubo un problema al obtener los pedidos de revisión. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && cantidad === 0 && (
        <InlineAlert severity="info" title="No hay pedidos para revisar">
          Cuando tu ámbito tenga pedidos en revisión, vas a verlos acá organizados por etapa.
        </InlineAlert>
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
