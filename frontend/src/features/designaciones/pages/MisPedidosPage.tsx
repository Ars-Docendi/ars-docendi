import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert, Modal } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaMisPedidos } from "../components/TablaMisPedidos";
import { useActorContexto } from "../hooks/useActorContexto";
import { useMisPedidos } from "../hooks/usePedidos";
import { useCancelarPedido, useEnviarPedido, useReenviarPedido } from "../hooks/useAccionesPedido";
import type { PedidoDesignacion } from "../types";

export function MisPedidosPage() {
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { data: pedidos, isLoading, isError } = useMisPedidos(actor);
  const enviar = useEnviarPedido(actor);
  const cancelar = useCancelarPedido(actor);
  const reenviar = useReenviarPedido(actor);

  const [pedidoACancelar, setPedidoACancelar] = useState<PedidoDesignacion | undefined>();

  function handleEditar(pedido: PedidoDesignacion) {
    navegar(`/designaciones/pedidos/${pedido.id}/editar`);
  }

  function handleConfirmarCancelar() {
    if (pedidoACancelar) {
      cancelar.mutate(pedidoACancelar.id);
    }
    setPedidoACancelar(undefined);
  }

  const cantidad = pedidos?.length ?? 0;

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
        pretitle="Proyecto docente · período abierto"
        title="Mis pedidos"
        meta={isLoading ? "Cargando…" : `${cantidad} pedido${cantidad !== 1 ? "s" : ""}`}
        actions={
          <Button variant="primary" onClick={() => navegar("/designaciones/pedidos/nuevo")}>
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

      {!isLoading && !isError && cantidad === 0 && (
        <InlineAlert severity="info" title="Todavía no cargaste pedidos">
          Empezá creando el primer pedido de designación del período con “Nuevo pedido”.
        </InlineAlert>
      )}

      {!isLoading && !isError && cantidad > 0 && pedidos && (
        <TablaMisPedidos
          pedidos={pedidos}
          onEditar={handleEditar}
          onEnviar={(pedido) => enviar.mutate(pedido.id)}
          onCancelar={(pedido) => setPedidoACancelar(pedido)}
          onReenviar={(pedido) => reenviar.mutate(pedido.id)}
        />
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
