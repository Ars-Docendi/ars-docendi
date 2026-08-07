import { useState } from "react";
import type { ReactElement } from "react";
import { Button, Modal, Textarea } from "@ars-docendi/ui";
import type { ButtonVariant } from "@ars-docendi/ui";
import type { EstadoPedido, Novedad, PedidoDesignacion } from "../types";

/** Acciones de revisión que se confirman con este modal. */
export type AccionRevision = "aceptar" | "rechazar" | "devolver" | "priorizar" | "despriorizar";

/** Tono visual de las cajas (header e info/aviso), mapeado a tokens del design system. */
type Tono = "accent" | "danger" | "warning";

// ---- Iconos Lucide (viewBox 24, stroke currentColor) — tal cual el screens.pen ----
function IconoLucide({ children }: { children: ReactElement | ReactElement[] }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

const ICONO_CHECK = (
  <IconoLucide>
    <path d="M20 6 9 17l-5-5" />
  </IconoLucide>
);
const ICONO_UNDO = (
  <IconoLucide>
    <path d="M9 14 4 9l5-5" />
    <path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H10" />
  </IconoLucide>
);
const ICONO_CORNER_UP_LEFT = (
  <IconoLucide>
    <path d="M9 14 4 9l5-5" />
    <path d="M20 20v-7a4 4 0 0 0-4-4H4" />
  </IconoLucide>
);
const ICONO_FLAG = (
  <IconoLucide>
    <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" />
    <line x1="4" x2="4" y1="22" y2="15" />
  </IconoLucide>
);
const ICONO_FLECHA_CIRCULO = (
  <IconoLucide>
    <circle cx="12" cy="12" r="10" />
    <path d="m12 16 4-4-4-4" />
    <path d="M8 12h8" />
  </IconoLucide>
);
const ICONO_ALERTA = (
  <IconoLucide>
    <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
    <path d="M12 9v4" />
    <path d="M12 17h.01" />
  </IconoLucide>
);
const ICONO_FLAG_OFF = (
  <IconoLucide>
    <path d="M8 2c3 0 5 2 8 2s3.5-1 5-2v10" />
    <path d="M11.53 15.929A19.478 19.478 0 0 0 8 15.5c-2.5 0-3.5 1-5 2" />
    <path d="M4 22V4" />
    <path d="m2 2 20 20" />
  </IconoLucide>
);

// ---- Textos dependientes de la etapa actual del pedido ----
const ETAPA_ACTUAL_LABEL: Partial<Record<EstadoPedido, string>> = {
  en_revision_coordinador: "Coordinador",
  en_revision_secretaria: "Secretaría",
  en_revision_decanato: "Decanato",
};

/** Subtítulo "pasa a …" y aviso de aceptación, según la etapa que avanza. */
const ACEPTAR_POR_ETAPA: Partial<Record<EstadoPedido, { destino: string; aviso: string }>> = {
  en_revision_coordinador: {
    destino: "Secretaría",
    aviso:
      "El pedido pasará a En revisión Secretaría y se notificará a Secretaría Académica (in-app).",
  },
  en_revision_secretaria: {
    destino: "Decanato",
    aviso: "El pedido pasará a En revisión Decanato y se notificará al Decanato (in-app).",
  },
  en_revision_decanato: {
    destino: "lote",
    aviso: "El pedido pasará a En lote y queda listo para su procesamiento.",
  },
};

/** A quién vuelve el pedido al devolver, según la etapa actual [BR-014]. */
const DEVOLUCION_POR_ETAPA: Partial<
  Record<EstadoPedido, { vuelveA: string; propietario: string }>
> = {
  en_revision_coordinador: { vuelveA: "al Jefe de Cátedra", propietario: "el Jefe de Cátedra" },
  en_revision_secretaria: { vuelveA: "al Coordinador", propietario: "el Coordinador" },
  en_revision_decanato: { vuelveA: "a la Secretaría", propietario: "la Secretaría" },
};

/** Frase de la novedad para el cuerpo del modal. */
const NOVEDAD_FRASE: Record<Novedad, string> = {
  "Cambio de cargo o dedicación": "el cambio de cargo o dedicación",
  Alta: "el alta del docente",
  Baja: "la baja del docente",
  "Sin novedad": "el pedido",
};

interface ConfigAccion {
  titulo: string;
  subtitulo: string;
  tonoHeader: Tono;
  icono: ReactElement;
  cuerpo: string;
  aviso: { tono: Tono; icono: ReactElement; texto: string } | null;
  etiquetaCampo: string;
  requiereJustificativo: boolean;
  placeholder: string;
  etiquetaConfirmar: string;
  varianteConfirmar: ButtonVariant;
  iconoConfirmar: ReactElement;
}

function construirConfig(accion: AccionRevision, pedido: PedidoDesignacion): ConfigAccion {
  const etapaActual = ETAPA_ACTUAL_LABEL[pedido.estado] ?? "tu etapa";
  const novedadFrase = NOVEDAD_FRASE[pedido.novedad];
  const sujeto = `${novedadFrase} de Prof. ${pedido.docente.nombre} (${pedido.catedra})`;

  switch (accion) {
    case "aceptar": {
      const avance = ACEPTAR_POR_ETAPA[pedido.estado];
      const destino = avance?.destino ?? "la etapa siguiente";
      return {
        titulo: "Aceptar pedido",
        subtitulo: `Etapa ${etapaActual} · pasa a ${destino}`,
        tonoHeader: "accent",
        icono: ICONO_CHECK,
        cuerpo: `Vas a aceptar ${sujeto}.`,
        aviso: {
          tono: "accent",
          icono: ICONO_FLECHA_CIRCULO,
          texto: avance?.aviso ?? "El pedido avanzará a la etapa siguiente del circuito.",
        },
        etiquetaCampo: "Comentario",
        requiereJustificativo: false,
        placeholder: "Agregá una observación para la próxima etapa (opcional)…",
        etiquetaConfirmar: "Aprobar y enviar",
        varianteConfirmar: "primary",
        iconoConfirmar: ICONO_CHECK,
      };
    }
    case "rechazar":
      return {
        titulo: "Rechazar pedido",
        subtitulo: "Termina el trámite · estado Rechazado",
        tonoHeader: "danger",
        icono: ICONO_UNDO,
        cuerpo: `Vas a rechazar ${sujeto}.`,
        aviso: {
          tono: "warning",
          icono: ICONO_ALERTA,
          texto:
            "Este pedido termina su trámite y queda en estado Rechazado. Para insistir, se genera un pedido nuevo. Se notifica al Jefe de Cátedra (in-app).",
        },
        etiquetaCampo: "Justificativo",
        requiereJustificativo: true,
        placeholder: "Indicá el motivo del rechazo. El Jefe de Cátedra lo verá completo.",
        etiquetaConfirmar: "Rechazar novedad",
        varianteConfirmar: "destructive",
        iconoConfirmar: ICONO_UNDO,
      };
    case "devolver": {
      const devolucion = DEVOLUCION_POR_ETAPA[pedido.estado] ?? {
        vuelveA: "al actor anterior",
        propietario: "el actor anterior",
      };
      return {
        titulo: "Devolver pedido",
        subtitulo: `Vuelve ${devolucion.vuelveA} · estado Devuelto`,
        tonoHeader: "warning",
        icono: ICONO_CORNER_UP_LEFT,
        cuerpo: `Vas a devolver ${sujeto}.`,
        aviso: {
          tono: "warning",
          icono: ICONO_ALERTA,
          texto: `El pedido vuelve a Borrador para que ${devolucion.propietario} lo corrija y lo reenvíe. Se notifica al destinatario (in-app).`,
        },
        etiquetaCampo: "Justificativo",
        requiereJustificativo: true,
        placeholder: "Indicá qué hay que corregir. El Jefe de Cátedra lo verá completo.",
        etiquetaConfirmar: "Devolver a Borrador",
        varianteConfirmar: "warning",
        iconoConfirmar: ICONO_CORNER_UP_LEFT,
      };
    }
    case "priorizar":
      return {
        titulo: "Marcar prioritario",
        subtitulo: "Cualquier actor · requiere justificativo",
        tonoHeader: "warning",
        icono: ICONO_FLAG,
        cuerpo:
          "Marcás este pedido como prioritario para comunicar la urgencia a los demás niveles del circuito. Requiere un justificativo.",
        aviso: null,
        etiquetaCampo: "Justificativo",
        requiereJustificativo: true,
        placeholder: "Aclará por qué tiene esta prioridad…",
        etiquetaConfirmar: "Guardar prioridad",
        varianteConfirmar: "primary",
        iconoConfirmar: ICONO_FLAG,
      };
    case "despriorizar":
      return {
        titulo: "Quitar prioridad",
        subtitulo: "Cualquier actor · sin justificativo",
        tonoHeader: "accent",
        icono: ICONO_FLAG_OFF,
        cuerpo: "Le sacás la marca de prioritario a este pedido.",
        aviso: {
          tono: "accent",
          icono: ICONO_FLAG_OFF,
          texto: "El pedido deja de figurar como prioritario para el resto del circuito.",
        },
        etiquetaCampo: "Comentario",
        requiereJustificativo: false,
        placeholder: "Agregá una aclaración (opcional)…",
        etiquetaConfirmar: "Quitar prioridad",
        varianteConfirmar: "primary",
        iconoConfirmar: ICONO_FLAG_OFF,
      };
  }
}

interface ModalConfirmacionAccionProps {
  /** Acción a confirmar; `null` cierra el modal. */
  accion: AccionRevision | null;
  pedido: PedidoDesignacion;
  /** Comentario tipeado en el panel inline, pre-cargado y editable en el modal. */
  comentarioInicial?: string;
  /** Hay una mutation en vuelo. */
  enviando?: boolean;
  onConfirmar: (comentario: string) => void;
  onCerrar: () => void;
}

/**
 * Modal de confirmación de una acción de revisión (Aceptar / Rechazar /
 * Devolver / Priorizar / Quitar prioritario), 1:1 con `screens.pen`. Es presentación pura: el page
 * dueño del estado dispara la mutation al confirmar. Aplica [BR-005]/[BR-017]
 * bloqueando el confirmar mientras falte el justificativo obligatorio; el
 * dominio sigue siendo la autoridad y revalida en la máquina de estados.
 */
export function ModalConfirmacionAccion({ accion, ...resto }: ModalConfirmacionAccionProps) {
  if (!accion) {
    return null;
  }
  return <Contenido key={accion} accion={accion} {...resto} />;
}

interface ContenidoProps extends Omit<ModalConfirmacionAccionProps, "accion"> {
  accion: AccionRevision;
}

function Contenido({
  accion,
  pedido,
  comentarioInicial = "",
  enviando = false,
  onConfirmar,
  onCerrar,
}: ContenidoProps) {
  const [comentario, setComentario] = useState(comentarioInicial);
  const config = construirConfig(accion, pedido);

  const faltaJustificativo = config.requiereJustificativo && comentario.trim() === "";

  function handleConfirmar() {
    if (faltaJustificativo) {
      return;
    }
    onConfirmar(comentario.trim());
  }

  return (
    <Modal
      open
      className="adoc-macc"
      onOpenChange={(abierto) => {
        if (!abierto) onCerrar();
      }}
      title={
        <span className="adoc-macc-head">
          <span className={`adoc-macc-ico adoc-macc-ico--${config.tonoHeader}`}>
            {config.icono}
          </span>
          <span className="adoc-macc-titles">
            <span className="adoc-macc-title">{config.titulo}</span>
            <span className="adoc-macc-sub">{config.subtitulo}</span>
          </span>
        </span>
      }
      footer={
        <>
          <Button variant="secondary" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Button>
          <Button
            variant={config.varianteConfirmar}
            onClick={handleConfirmar}
            loading={enviando}
            disabled={enviando || faltaJustificativo}
          >
            <span className="adoc-macc-btnico">{config.iconoConfirmar}</span>
            {config.etiquetaConfirmar}
          </Button>
        </>
      }
    >
      <div className="adoc-macc-body">
        <p className="adoc-macc-text">{config.cuerpo}</p>

        {config.aviso && (
          <div className={`adoc-macc-aviso adoc-macc-aviso--${config.aviso.tono}`}>
            <span className="adoc-macc-aviso-ico">{config.aviso.icono}</span>
            <span>{config.aviso.texto}</span>
          </div>
        )}

        <div className="adoc-macc-field">
          <div className="adoc-macc-lblrow">
            <label htmlFor="comentario-accion-revision">{config.etiquetaCampo}</label>
            <span className={config.requiereJustificativo ? "adoc-macc-req" : "adoc-macc-opt"}>
              {config.requiereJustificativo ? "· obligatorio" : "opcional"}
            </span>
          </div>
          <Textarea
            id="comentario-accion-revision"
            value={comentario}
            onChange={(e) => setComentario(e.target.value)}
            placeholder={config.placeholder}
            rows={3}
            invalid={false}
          />
        </div>
      </div>
    </Modal>
  );
}
