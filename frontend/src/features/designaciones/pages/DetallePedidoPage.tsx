import { useState } from "react";
import { useParams } from "react-router-dom";
import {
  ApprovalTimeline,
  AuditLog,
  Breadcrumbs,
  Button,
  DataList,
  InlineAlert,
  Tabs,
} from "@ars-docendi/ui";
import type { DataListItem, TabItem } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { EstadoPedidoBadge } from "../components/EstadoPedidoBadge";
import { ModalAccionRevision } from "../components/ModalAccionRevision";
import type { AccionRevision } from "../components/ModalAccionRevision";
import { derivarTimeline, historialAAuditEntries } from "../components/detalleAdapters";
import { actorAlcanzaAmbito, puedeAceptar, puedeRevisar } from "../api/maquinaEstados";
import { useActorContexto } from "../hooks/useActorContexto";
import { usePedido } from "../hooks/usePedidos";
import {
  useAceptarPedido,
  useDevolverPedido,
  usePriorizarPedido,
  useRechazarPedido,
} from "../hooks/useAccionesPedido";
import type { Adjunto, PedidoDesignacion } from "../types";

const RUTA_REVISION = "/designaciones/revision";

const ETIQUETA_ADJUNTO: Record<Adjunto["tipo"], string> = {
  cv: "CV",
  dni_frente: "DNI (frente)",
  dni_dorso: "DNI (dorso)",
  justificativo: "Justificativo",
};

function itemsDatos(pedido: PedidoDesignacion): DataListItem[] {
  const items: DataListItem[] = [
    { term: "Docente", description: `${pedido.docente.nombre} (DNI ${pedido.docente.dni})` },
    { term: "Antigüedad", description: `${pedido.docente.antiguedad} años` },
    { term: "Cátedra", description: pedido.catedra },
    { term: "Carrera", description: pedido.carrera },
    { term: "Materia asociada", description: pedido.materiaAsociada },
    { term: "Novedad", description: pedido.novedad },
    { term: "Cargo actual", description: pedido.cargoActual ?? "—" },
    { term: "Dedicación actual", description: pedido.dedicacionActual ?? "—" },
  ];
  if (pedido.cargoSolicitado) {
    items.push({ term: "Cargo solicitado", description: pedido.cargoSolicitado });
  }
  if (pedido.dedicacionSolicitada) {
    items.push({ term: "Dedicación solicitada", description: pedido.dedicacionSolicitada });
  }
  if (pedido.justificacion) {
    items.push({ term: "Justificación", description: pedido.justificacion });
  }
  items.push(
    { term: "Horas de investigación", description: `${pedido.horasInvestigacion ?? 0} h` },
    { term: "Hace horas en otro Depto.", description: pedido.haceHorasOtroDepto ? "Sí" : "No" },
  );
  return items;
}

export function DetallePedidoPage() {
  const { id } = useParams();
  const actor = useActorContexto();
  const { data: pedido, isLoading, isError } = usePedido(id);
  const [tab, setTab] = useState("solicitud");
  const [accion, setAccion] = useState<AccionRevision | null>(null);

  const aceptar = useAceptarPedido(actor);
  const rechazar = useRechazarPedido(actor);
  const devolver = useDevolverPedido(actor);
  const priorizar = usePriorizarPedido(actor);
  const enviando =
    aceptar.isPending || rechazar.isPending || devolver.isPending || priorizar.isPending;

  function handleConfirmar(comentario: string) {
    if (!pedido) return;
    const cerrar = () => setAccion(null);
    const ref = pedido.id;
    if (accion === "aceptar") {
      aceptar.mutate({ id: ref, comentario: comentario || undefined }, { onSuccess: cerrar });
    } else if (accion === "rechazar") {
      rechazar.mutate({ id: ref, comentario }, { onSuccess: cerrar });
    } else if (accion === "devolver") {
      devolver.mutate({ id: ref, comentario }, { onSuccess: cerrar });
    } else if (accion === "priorizar") {
      priorizar.mutate({ id: ref, comentario }, { onSuccess: cerrar });
    }
  }

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

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando el pedido…</p>}

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
          esRevisor={puedeRevisar(pedido, actor)}
          permiteAceptar={puedeAceptar(pedido, actor)}
          tab={tab}
          onTab={setTab}
          enviando={enviando}
          accion={accion}
          onAbrirAccion={setAccion}
          onCerrarAccion={() => setAccion(null)}
          onConfirmar={handleConfirmar}
        />
      )}
    </>
  );
}

interface DetalleCargadoProps {
  pedido: PedidoDesignacion;
  esRevisor: boolean;
  permiteAceptar: boolean;
  tab: string;
  onTab: (id: string) => void;
  enviando: boolean;
  accion: AccionRevision | null;
  onAbrirAccion: (accion: AccionRevision) => void;
  onCerrarAccion: () => void;
  onConfirmar: (comentario: string) => void;
}

function DetalleCargado({
  pedido,
  esRevisor,
  permiteAceptar,
  tab,
  onTab,
  enviando,
  accion,
  onAbrirAccion,
  onCerrarAccion,
  onConfirmar,
}: DetalleCargadoProps) {
  const tabs: TabItem[] = [
    { id: "solicitud", label: "Solicitud" },
    { id: "historial", label: "Historial", count: pedido.historial.length },
    { id: "documentos", label: "Documentos", count: pedido.adjuntos.length },
  ];

  return (
    <>
      <PageHeader
        pretitle="Circuito de aprobación"
        title={`Pedido de ${pedido.docente.nombre}`}
        meta={<EstadoPedidoBadge estado={pedido.estado} prioritario={pedido.prioritario} />}
        actions={
          esRevisor && (
            <div style={{ display: "flex", gap: "var(--space-1)", flexWrap: "wrap" }}>
              {permiteAceptar && (
                <Button variant="primary" onClick={() => onAbrirAccion("aceptar")}>
                  Aceptar
                </Button>
              )}
              <Button variant="destructive" onClick={() => onAbrirAccion("rechazar")}>
                Rechazar
              </Button>
              <Button variant="warning" onClick={() => onAbrirAccion("devolver")}>
                Devolver
              </Button>
              <Button variant="ghost" onClick={() => onAbrirAccion("priorizar")}>
                Marcar prioritario
              </Button>
            </div>
          )
        }
      />

      <Tabs items={tabs} value={tab} onChange={onTab} aria-label="Secciones del pedido" />

      {tab === "solicitud" && (
        <div style={{ display: "grid", gap: "var(--space-4)", marginTop: "var(--space-3)" }}>
          <DataList items={itemsDatos(pedido)} />
          <section aria-label="Cadena de aprobación">
            <h2 style={{ fontSize: "var(--text-body-size)" }}>Cadena de aprobación</h2>
            <ApprovalTimeline steps={derivarTimeline(pedido)} />
          </section>
        </div>
      )}

      {tab === "historial" && (
        <div style={{ marginTop: "var(--space-3)" }}>
          <AuditLog entries={historialAAuditEntries(pedido.historial)} />
        </div>
      )}

      {tab === "documentos" && (
        <div style={{ marginTop: "var(--space-3)" }}>
          {pedido.adjuntos.length === 0 ? (
            <InlineAlert severity="info" title="Sin documentos adjuntos">
              Este pedido no tiene documentación adjunta.
            </InlineAlert>
          ) : (
            <ul>
              {pedido.adjuntos.map((adjunto) => (
                <li key={adjunto.id}>
                  <strong>{ETIQUETA_ADJUNTO[adjunto.tipo]}</strong> — {adjunto.nombre}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <ModalAccionRevision
        accion={accion}
        pedido={pedido}
        enviando={enviando}
        onCerrar={onCerrarAccion}
        onConfirmar={onConfirmar}
      />
    </>
  );
}
