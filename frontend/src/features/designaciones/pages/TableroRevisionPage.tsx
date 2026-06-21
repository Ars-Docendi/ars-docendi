import { useNavigate } from "react-router-dom";
import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TableroRevision } from "../components/TableroRevision";
import { useActorContexto } from "../hooks/useActorContexto";
import { usePedidosPorAmbito } from "../hooks/usePedidos";
import type { PedidoDesignacion } from "../types";

export function TableroRevisionPage() {
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { data: pedidos, isLoading, isError } = usePedidosPorAmbito(actor);

  function handleSeleccionar(pedido: PedidoDesignacion) {
    navegar(`/designaciones/pedidos/${pedido.id}`);
  }

  const cantidad = pedidos?.length ?? 0;

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones", href: "/designaciones" },
          { label: "Revisión" },
        ]}
      />
      <PageHeader
        pretitle="Circuito de aprobación"
        title="Revisión de pedidos"
        meta={
          isLoading ? "Cargando…" : `${cantidad} pedido${cantidad !== 1 ? "s" : ""} en tu ámbito`
        }
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
          Cuando tu ámbito tenga pedidos en revisión, vas a verlos acá organizados por estado.
        </InlineAlert>
      )}

      {!isLoading && !isError && cantidad > 0 && pedidos && (
        <TableroRevision pedidos={pedidos} actor={actor} onSeleccionar={handleSeleccionar} />
      )}
    </>
  );
}
