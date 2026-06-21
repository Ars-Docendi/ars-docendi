import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, InlineAlert, Select } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TableroRevision } from "../components/TableroRevision";
import { TablaRevision } from "../components/TablaRevision";
import { SwitchVista } from "../components/SwitchVista";
import type { VistaActiva } from "../components/SwitchVista";
import { FILTROS_INICIALES } from "../components/filtrosTablero";
import type {
  FiltroPrioridad,
  FiltroTipo,
  FiltrosTablero,
  VistaTablero,
} from "../components/filtrosTablero";
import { useActorContexto } from "../hooks/useActorContexto";
import { usePedidosPorAmbito } from "../hooks/usePedidos";
import type { PedidoDesignacion } from "../types";

export function TableroRevisionPage() {
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { data: pedidos, isLoading, isError } = usePedidosPorAmbito(actor);
  const [filtros, setFiltros] = useState<FiltrosTablero>(FILTROS_INICIALES);
  const [vistaActiva, setVistaActiva] = useState<VistaActiva>("tablero");

  function handleSeleccionar(pedido: PedidoDesignacion) {
    navegar(`/designaciones/pedidos/${pedido.id}`);
  }

  const cantidad = pedidos?.length ?? 0;
  const ambito = actor.carrera ?? "Departamento";

  const filtrosUI = (
    <div className="adoc-tablero-filtros">
      <SwitchVista vista={vistaActiva} onCambiar={setVistaActiva} />
      <Select
        aria-label="Filtrar pedidos por turno"
        wrapClassName="adoc-filtro-activo"
        value={filtros.vista}
        onChange={(e) => setFiltros((f) => ({ ...f, vista: e.target.value as VistaTablero }))}
      >
        <option value="completa">Vista completa</option>
        <option value="mis-pendientes">Mis pendientes</option>
      </Select>
      <Select
        aria-label="Filtrar por tipo de novedad"
        value={filtros.tipo}
        onChange={(e) => setFiltros((f) => ({ ...f, tipo: e.target.value as FiltroTipo }))}
      >
        <option value="todos">Tipo: Todos</option>
        <option value="Sin novedad">Sin novedad</option>
        <option value="Alta">Alta</option>
        <option value="Baja">Baja</option>
        <option value="Cambio de cargo o dedicación">Cambio</option>
      </Select>
      <Select
        aria-label="Filtrar por prioridad"
        value={filtros.prioridad}
        onChange={(e) =>
          setFiltros((f) => ({ ...f, prioridad: e.target.value as FiltroPrioridad }))
        }
      >
        <option value="todos">Prioritario: Todos</option>
        <option value="prioritarios">Solo prioritarios</option>
        <option value="normales">Sin prioridad</option>
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
        actions={!isLoading && !isError && cantidad > 0 ? filtrosUI : undefined}
      />

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

      {!isLoading &&
        !isError &&
        cantidad > 0 &&
        pedidos &&
        (vistaActiva === "tabla" ? (
          <TablaRevision
            pedidos={pedidos}
            actor={actor}
            filtros={filtros}
            onSeleccionar={handleSeleccionar}
          />
        ) : (
          <TableroRevision
            pedidos={pedidos}
            actor={actor}
            filtros={filtros}
            onSeleccionar={handleSeleccionar}
          />
        ))}
    </>
  );
}
