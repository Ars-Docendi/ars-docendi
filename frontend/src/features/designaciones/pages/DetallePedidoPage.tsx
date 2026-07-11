import { useState } from "react";
import { useParams } from "react-router-dom";
import { AuditLog, Breadcrumbs, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { EstadoPedidoBadge } from "../components/EstadoPedidoBadge";
import { CadenaRevision } from "../components/CadenaRevision";
import { ResumenPedido } from "../components/ResumenPedido";
import { PanelAccionesRevision } from "../components/PanelAccionesRevision";
import {
  ModalConfirmacionAccion,
  type AccionRevision,
} from "../components/ModalConfirmacionAccion";
import { DatosTramite } from "../components/DatosTramite";
import {
  derivarCadena,
  historialAAuditEntries,
  resumenMaterias,
} from "../components/detalleAdapters";
import { actorAlcanzaAmbito, puedeAceptar, puedeRevisar } from "../api/maquinaEstados";
import { PERIODOS_MOCK } from "../api/periodosMock";
import { useActorContexto } from "../hooks/useActorContexto";
import { usePedido } from "../hooks/usePedidos";
import {
  useAceptarPedido,
  useDevolverPedido,
  usePriorizarPedido,
  useRechazarPedido,
} from "../hooks/useAccionesPedido";
import type { ActorContexto, Novedad, PedidoDesignacion } from "../types";
import "./detalle.css";

const RUTA_REVISION = "/designaciones/revision";

/** Título legible de la novedad para el header del detalle. */
const TITULO_NOVEDAD: Record<Novedad, string> = {
  Alta: "Alta de docente",
  Baja: "Baja de docente",
  "Cambio de cargo o dedicación": "Cambio de cargo o dedicación",
  "Sin novedad": "Sin novedad",
};

export function DetallePedidoPage() {
  const { id } = useParams();
  const actor = useActorContexto();
  const { data: pedido, isLoading, isError } = usePedido(id);

  const aceptar = useAceptarPedido(actor);
  const rechazar = useRechazarPedido(actor);
  const devolver = useDevolverPedido(actor);
  const priorizar = usePriorizarPedido(actor);
  const enviando =
    aceptar.isPending || rechazar.isPending || devolver.isPending || priorizar.isPending;

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones", href: "/designaciones" },
          { label: "Revisión", href: RUTA_REVISION },
          { label: "Detalle del pedido" },
        ]}
      />

      {isLoading && (
        <p role="status" aria-live="polite" style={{ color: "var(--color-text-secondary)" }}>
          Cargando el pedido…
        </p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se encontró el pedido">
          No pudimos cargar el pedido solicitado. <a href={RUTA_REVISION}>Volver a Revisión</a>.
        </InlineAlert>
      )}

      {pedido && !actorAlcanzaAmbito(pedido, actor) && (
        <InlineAlert severity="info" title="Este pedido está fuera de tu ámbito">
          No tenés visibilidad sobre este pedido. <a href={RUTA_REVISION}>Volver a Revisión</a>.
        </InlineAlert>
      )}

      {pedido && actorAlcanzaAmbito(pedido, actor) && (
        <DetalleCargado
          pedido={pedido}
          actor={actor}
          enviando={enviando}
          onAceptar={(comentario) => aceptar.mutate({ id: pedido.id, comentario })}
          onRechazar={(comentario) => rechazar.mutate({ id: pedido.id, comentario })}
          onDevolver={(comentario) => devolver.mutate({ id: pedido.id, comentario })}
          onPriorizar={(comentario) => priorizar.mutate({ id: pedido.id, comentario })}
        />
      )}
    </>
  );
}

interface DetalleCargadoProps {
  pedido: PedidoDesignacion;
  actor: ActorContexto;
  enviando: boolean;
  onAceptar: (comentario?: string) => void;
  onRechazar: (comentario: string) => void;
  onDevolver: (comentario: string) => void;
  onPriorizar: (comentario: string) => void;
}

function DetalleCargado({
  pedido,
  actor,
  enviando,
  onAceptar,
  onRechazar,
  onDevolver,
  onPriorizar,
}: DetalleCargadoProps) {
  const esRevisor = puedeRevisar(pedido, actor);
  const permiteAceptar = puedeAceptar(pedido, actor);
  const etapas = derivarCadena(pedido, actor);
  const periodoNombre = PERIODOS_MOCK.find((p) => p.id === pedido.periodoId)?.nombre;

  // Acción a confirmar (abre el modal) + comentario compartido panel↔modal.
  const [accionPendiente, setAccionPendiente] = useState<AccionRevision | null>(null);
  const [comentario, setComentario] = useState("");

  function ejecutar(accion: AccionRevision, texto: string) {
    switch (accion) {
      case "aceptar":
        onAceptar(texto || undefined);
        break;
      case "rechazar":
        onRechazar(texto);
        break;
      case "devolver":
        onDevolver(texto);
        break;
      case "priorizar":
        onPriorizar(texto);
        break;
    }
  }

  function confirmar(texto: string) {
    if (accionPendiente) {
      ejecutar(accionPendiente, texto);
    }
    setAccionPendiente(null);
    setComentario("");
  }

  return (
    <>
      <PageHeader
        pretitle={`Designaciones · Pedido ${pedido.id.toUpperCase()}`}
        title={`${TITULO_NOVEDAD[pedido.novedad]} — ${resumenMaterias(pedido.asignaciones)}`}
        meta={`Cátedra ${pedido.catedra} · ${pedido.carrera}${periodoNombre ? ` · ${periodoNombre}` : ""}`}
        actions={<EstadoPedidoBadge estado={pedido.estado} prioritario={pedido.prioritario} />}
      />

      <div className="adoc-det">
        <CadenaRevision etapas={etapas} />

        <div className="adoc-det-cols">
          <div className="adoc-det-main">
            <ResumenPedido pedido={pedido} periodoNombre={periodoNombre} />

            <section className="adoc-det-hist" aria-label="Historial del pedido">
              <header className="adoc-hist-head">
                <h2 className="adoc-hist-title">Historial del pedido</h2>
                <span className="adoc-hist-note">Auditoría · usuario · fecha (RNF-7)</span>
              </header>
              <AuditLog entries={historialAAuditEntries(pedido.historial)} />
            </section>
          </div>

          <aside className="adoc-det-rail">
            {esRevisor && (
              <PanelAccionesRevision
                pedido={pedido}
                actor={actor}
                permiteAceptar={permiteAceptar}
                enviando={enviando}
                comentario={comentario}
                onComentarioChange={setComentario}
                onSolicitarAccion={(accion) => setAccionPendiente(accion)}
              />
            )}
            <DatosTramite pedido={pedido} />
          </aside>
        </div>
      </div>

      <ModalConfirmacionAccion
        accion={accionPendiente}
        pedido={pedido}
        comentarioInicial={comentario}
        enviando={enviando}
        onConfirmar={confirmar}
        onCerrar={() => setAccionPendiente(null)}
      />
    </>
  );
}
