import { Button, Textarea } from "@ars-docendi/ui";
import type { ActorContexto, EstadoPedido, PedidoDesignacion } from "../types";
import type { AccionRevision } from "./ModalConfirmacionAccion";

/** Etiqueta del botón "Aprobar" según la etapa siguiente. */
const ETIQUETA_APROBAR: Partial<Record<EstadoPedido, string>> = {
  en_revision_coordinador: "Aprobar y pasar a Secretaría",
  en_revision_secretaria: "Aprobar y pasar a Decanato",
  en_revision_decanato: "Aprobar y enviar a lote",
};

const ICONO_ESCUDO = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.4" aria-hidden="true">
    <path
      d="M8 1.5 3 3.5v3.2C3 9.8 5.1 12.5 8 13.5c2.9-1 5-3.7 5-6.8V3.5z"
      strokeLinejoin="round"
    />
    <path d="m6 7.5 1.5 1.5L10.5 6" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const BLOQUE = { width: "100%" } as const;

interface PanelAccionesRevisionProps {
  pedido: PedidoDesignacion;
  actor: ActorContexto;
  /** El actor puede aprobar (avanzar la cadena). Administración no aprueba [BR-015]. */
  permiteAceptar: boolean;
  /** Hay una mutation en vuelo. */
  enviando: boolean;
  /** Comentario/justificativo (entrada rápida); se traslada al modal de confirmación. */
  comentario: string;
  onComentarioChange: (valor: string) => void;
  /** Solicita confirmar una acción: abre el modal correspondiente (no muta todavía). */
  onSolicitarAccion: (accion: AccionRevision) => void;
}

/**
 * Panel de acciones de revisión (rail derecho). Los botones ya no mutan ni
 * validan inline: abren el modal de confirmación ([BR-005]/[BR-017] se validan
 * ahí). El `Textarea` queda como entrada rápida del justificativo, que viaja
 * pre-cargado al modal. El dominio sigue siendo la autoridad.
 */
export function PanelAccionesRevision({
  pedido,
  actor,
  permiteAceptar,
  enviando,
  comentario,
  onComentarioChange,
  onSolicitarAccion,
}: PanelAccionesRevisionProps) {
  const etiquetaAprobar = ETIQUETA_APROBAR[pedido.estado] ?? "Aprobar";
  const ambito = actor.carrera ? `${actor.rol} de ${actor.carrera}` : actor.rol;

  return (
    <section className="adoc-card adoc-acc">
      <h2 className="adoc-acc-title">Acciones de revisión</h2>

      {permiteAceptar && (
        <Button
          variant="primary"
          style={BLOQUE}
          disabled={enviando}
          loading={enviando}
          onClick={() => onSolicitarAccion("aceptar")}
        >
          {etiquetaAprobar}
        </Button>
      )}

      <div className="adoc-acc-field">
        <div className="adoc-acc-lblrow">
          <label htmlFor="justificativo-revision">Justificativo</label>
          <span id="justificativo-help" className="adoc-acc-help">
            obligatorio para rechazar, devolver o marcar prioritario
          </span>
        </div>
        <Textarea
          id="justificativo-revision"
          value={comentario}
          onChange={(e) => onComentarioChange(e.target.value)}
          placeholder="Detallá el motivo de la acción…"
          rows={3}
          aria-describedby="justificativo-help"
        />
      </div>

      <div className="adoc-acc-row">
        {pedido.accionesPermitidas?.includes("rechazar") && (
          <Button
            variant="destructive"
            style={BLOQUE}
            disabled={enviando}
            onClick={() => onSolicitarAccion("rechazar")}
          >
            Rechazar
          </Button>
        )}
        {pedido.accionesPermitidas?.includes("devolver") && (
          <Button
            variant="secondary"
            style={BLOQUE}
            disabled={enviando}
            onClick={() => onSolicitarAccion("devolver")}
          >
            Devolver
          </Button>
        )}
      </div>

      {(pedido.accionesPermitidas?.includes("priorizar") ||
        pedido.accionesPermitidas?.includes("despriorizar")) && (
        <Button
          variant="ghost"
          style={BLOQUE}
          disabled={enviando}
          onClick={() => onSolicitarAccion(pedido.prioritario ? "despriorizar" : "priorizar")}
        >
          {pedido.prioritario ? "Quitar prioritario" : "Marcar prioritario"}
        </Button>
      )}

      <div className="adoc-divider" />

      <div className="adoc-scopehint">
        <span className="adoc-scopehint-ico">{ICONO_ESCUDO}</span>
        <span>
          Revisás como {ambito}. Solo podés actuar en tu etapa actual y dentro de tu ámbito.
        </span>
      </div>
    </section>
  );
}
