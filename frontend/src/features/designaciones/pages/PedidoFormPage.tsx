import { useNavigate, useParams } from "react-router-dom";
import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";
import { PedidoForm } from "../components/PedidoForm";
import { useActorContexto } from "../hooks/useActorContexto";
import { useMisPedidos, usePedido } from "../hooks/usePedidos";
import { useCrearPedido, useEditarPedido } from "../hooks/useAccionesPedido";
import { puedeEditarPedido } from "../api/maquinaEstados";
import { PERIODO_ABIERTO_ID } from "../api/pedidosSeed";
import { PERIODOS_MOCK } from "../api/periodosMock";
import type { DatosEditablesPedido } from "../types";

const RUTA_MIS_PEDIDOS = "/designaciones/mis-pedidos";

/** Etiqueta corta del período a partir de su id. */
function etiquetaPeriodo(periodoId: string): string {
  const periodo = PERIODOS_MOCK.find((item) => item.id === periodoId);
  return periodo?.nombre ?? "Período sin definir";
}

export function PedidoFormPage() {
  const { id } = useParams();
  const esEdicion = Boolean(id);
  const navegar = useNavigate();
  const actor = useActorContexto();

  const { data: pedidos } = useMisPedidos(actor);
  const { data: pedidoInicial, isLoading, isError } = usePedido(id);
  const crear = useCrearPedido(actor);
  const editar = useEditarPedido(actor);

  function volver() {
    navegar(RUTA_MIS_PEDIDOS);
  }

  function handleGuardar(datos: DatosEditablesPedido) {
    if (esEdicion && id) {
      editar.mutate({ id, datos }, { onSuccess: volver });
    } else {
      crear.mutate(datos, { onSuccess: volver });
    }
  }

  const guardando = crear.isPending || editar.isPending;
  const periodoLabel = etiquetaPeriodo(pedidoInicial?.periodoId ?? PERIODO_ABIERTO_ID);
  const crumbEdicion = pedidoInicial?.numero ? `Editar · ${pedidoInicial.numero}` : "Editar";

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones", href: "/designaciones" },
          { label: "Mis pedidos", href: RUTA_MIS_PEDIDOS },
          { label: esEdicion ? crumbEdicion : "Nuevo pedido" },
        ]}
      />

      {esEdicion && isLoading && (
        <p style={{ color: "var(--color-text-secondary)" }}>Cargando el pedido…</p>
      )}

      {esEdicion && isError && (
        <InlineAlert severity="danger" title="No se encontró el pedido">
          No pudimos cargar el pedido solicitado.{" "}
          <a href={RUTA_MIS_PEDIDOS}>Volver a Mis pedidos</a>.
        </InlineAlert>
      )}

      {esEdicion && pedidoInicial && !puedeEditarPedido(pedidoInicial, actor) && (
        <InlineAlert severity="info" title="Este pedido no es editable">
          El pedido ya fue enviado a revisión y quedó de solo lectura para el Jefe de Cátedra (salvo
          que sea devuelto). <a href={RUTA_MIS_PEDIDOS}>Volver a Mis pedidos</a>.
        </InlineAlert>
      )}

      {(crear.isError || editar.isError) && (
        <InlineAlert severity="danger" title="No se pudo guardar el pedido">
          Ocurrió un error al guardar. Revisá los datos e intentá de nuevo.
        </InlineAlert>
      )}

      {!esEdicion && (
        <PedidoForm
          pedidosExistentes={pedidos ?? []}
          periodoLabel={periodoLabel}
          guardando={guardando}
          onGuardar={handleGuardar}
          onCancelar={volver}
        />
      )}

      {esEdicion && pedidoInicial && puedeEditarPedido(pedidoInicial, actor) && (
        <PedidoForm
          pedidoInicial={pedidoInicial}
          pedidosExistentes={pedidos ?? []}
          esEdicion
          periodoLabel={periodoLabel}
          guardando={guardando}
          onGuardar={handleGuardar}
          onCancelar={volver}
        />
      )}
    </>
  );
}
