import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Button, AuditLog, InlineAlert } from "@ars-docendi/ui";
import { EnlaceVolver } from "../../../shared/navegacion/EnlaceVolver";
import { Migas } from "../../../shared/navegacion/Migas";
import { useDestinoVolver } from "../../../shared/navegacion/useDestinoVolver";
import {
  migaDeVolver,
  propagarVolver,
  type DestinoVolver,
} from "../../../shared/navegacion/volver";
import { useGuardiaDeCambios } from "../../../shared/navegacion/guardias";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { EstadoPedidoBadge } from "../components/EstadoPedidoBadge";
import { IconoSquarePen, IconoX } from "../components/lucide";
import { CadenaRevision } from "../components/CadenaRevision";
import { ResumenPedido } from "../components/ResumenPedido";
import { PanelAccionesRevision } from "../components/PanelAccionesRevision";
import {
  ModalConfirmacionAccion,
  type AccionRevision,
} from "../components/ModalConfirmacionAccion";
import { ModalConfirmarEliminar } from "../../../shared/ui/ModalConfirmarEliminar";
import { DatosTramite } from "../components/DatosTramite";
import { derivarCadena, historialAAuditEntries } from "../components/detalleAdapters";
import { useActorContexto } from "../hooks/useActorContexto";
import { usePedido } from "../hooks/usePedidos";
import { erroresValidacionPedido } from "../api/erroresValidacionPedido";
import {
  useAceptarPedido,
  useDespriorizarPedido,
  useDevolverPedido,
  useEliminarPedido,
  usePriorizarPedido,
  useRechazarPedido,
} from "../hooks/useAccionesPedido";
import type { ActorContexto, Novedad, PedidoDesignacion } from "../types";
import "./detalle.css";

/** Título legible de la novedad para el header del detalle. */
const TITULO_NOVEDAD: Record<Novedad, string> = {
  "Sin novedad": "Actualización de docente",
  Alta: "Alta de docente",
  Baja: "Baja de docente",
  "Cambio de cargo o dedicación": "Cambio de cargo o dedicación",
};

export function DetallePedidoPage() {
  const { id } = useParams();
  const navegar = useNavigate();
  const actor = useActorContexto();
  const { user } = useCurrentUser();
  const origen = useDestinoVolver("pedido");
  const { data: pedido, isLoading, isError } = usePedido(id);

  const aceptar = useAceptarPedido();
  const rechazar = useRechazarPedido();
  const devolver = useDevolverPedido();
  const priorizar = usePriorizarPedido();
  const despriorizar = useDespriorizarPedido();
  const eliminar = useEliminarPedido();
  const erroresAceptacion = erroresValidacionPedido(aceptar.error);
  const enviando =
    aceptar.isPending ||
    rechazar.isPending ||
    devolver.isPending ||
    priorizar.isPending ||
    despriorizar.isPending;

  return (
    <>
      <Migas
        conVolver
        items={[
          { label: "Inicio", href: "/" },
          migaDeVolver(origen),
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
          No pudimos cargar el pedido solicitado. <Link to={origen.ruta}>{origen.texto}</Link>.
        </InlineAlert>
      )}

      {aceptar.isError && (
        <InlineAlert severity="danger" title="No se pudo aprobar el pedido">
          {erroresAceptacion.length > 0 ? (
            <ul>
              {erroresAceptacion.map(({ campo, mensaje }, indice) => (
                <li key={`${campo}-${indice}`}>
                  <strong>{campo}:</strong> {mensaje}
                </li>
              ))}
            </ul>
          ) : (
            "No se pudo completar la aprobación. Actualizá el pedido e intentá de nuevo."
          )}
        </InlineAlert>
      )}

      {pedido && (
        <DetalleCargado
          pedido={pedido}
          actor={actor}
          conSeguimiento={user?.permissions.includes("designaciones.ver_seguimiento") ?? false}
          enviando={enviando}
          onAceptar={(comentario) => aceptar.mutate({ id: pedido.id, comentario })}
          onRechazar={(comentario) => rechazar.mutate({ id: pedido.id, comentario })}
          onDevolver={(comentario) => devolver.mutate({ id: pedido.id, comentario })}
          onPriorizar={(comentario) => priorizar.mutate({ id: pedido.id, comentario })}
          onDespriorizar={(comentario) => despriorizar.mutate({ id: pedido.id, comentario })}
          onEliminar={() =>
            // Al origen real (Revisión o Mis pedidos), reemplazando el detalle que ya no existe.
            eliminar.mutate(pedido.id, {
              onSuccess: () => navegar(origen.ruta, { replace: true }),
            })
          }
          eliminando={eliminar.isPending}
          errorEliminar={eliminar.isError ? eliminar.error.message : undefined}
          volver={origen}
          onEditar={() =>
            navegar(`/designaciones/pedidos/${pedido.id}/editar`, { state: propagarVolver(origen) })
          }
        />
      )}
    </>
  );
}

interface DetalleCargadoProps {
  pedido: PedidoDesignacion;
  actor: ActorContexto;
  /** Sin designaciones.ver_seguimiento no se muestran la cadena de etapas ni el historial. */
  conSeguimiento: boolean;
  enviando: boolean;
  onAceptar: (comentario?: string) => void;
  onRechazar: (comentario: string) => void;
  onDevolver: (comentario: string) => void;
  onPriorizar: (comentario: string) => void;
  onDespriorizar: (comentario?: string) => void;
  onEliminar: () => void;
  eliminando: boolean;
  errorEliminar?: string;
  volver: DestinoVolver;
  onEditar: () => void;
}

function DetalleCargado({
  pedido,
  actor,
  conSeguimiento,
  enviando,
  onAceptar,
  onRechazar,
  onDevolver,
  onPriorizar,
  onDespriorizar,
  onEliminar,
  eliminando,
  errorEliminar,
  volver,
  onEditar,
}: DetalleCargadoProps) {
  const acciones = pedido.accionesPermitidas ?? [];
  const esRevisor = acciones.some((accion) =>
    ["aceptar", "rechazar", "devolver", "priorizar", "despriorizar"].includes(accion),
  );
  const permiteAceptar = acciones.includes("aceptar");
  const puedeEditar = acciones.includes("editar");
  const puedeEliminar = acciones.includes("eliminar");
  const etapas = derivarCadena(pedido, actor);
  const periodoNombre = pedido.periodoNombre;

  // Acción a confirmar (abre el modal) + comentario compartido panel↔modal.
  const [accionPendiente, setAccionPendiente] = useState<AccionRevision | null>(null);
  const [comentario, setComentario] = useState("");
  const [mostrarEliminar, setMostrarEliminar] = useState(false);
  // El justificativo escrito en el panel se pierde al salir: se avisa.
  useGuardiaDeCambios(comentario.trim() !== "");

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
      case "despriorizar":
        onDespriorizar(texto || undefined);
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
        title={TITULO_NOVEDAD[pedido.novedad]}
        meta={periodoNombre}
        actions={
          <div className="adoc-det-headactions">
            <EnlaceVolver id="pedido-volver" destino={volver} />
            {puedeEditar && (
              <Button variant="secondary" leadingIcon={<IconoSquarePen />} onClick={onEditar}>
                Editar
              </Button>
            )}
            {puedeEliminar && (
              <Button
                variant="destructive"
                leadingIcon={<IconoX />}
                onClick={() => setMostrarEliminar(true)}
              >
                Eliminar
              </Button>
            )}
            <EstadoPedidoBadge estado={pedido.estado} prioritario={pedido.prioritario} />
          </div>
        }
      />

      <div className="adoc-det">
        {conSeguimiento && <CadenaRevision etapas={etapas} />}
        {pedido.estado === "devuelto" && pedido.motivoDevolucion && !conSeguimiento && (
          <InlineAlert severity="warning" title="Te devolvieron este pedido">
            «{pedido.motivoDevolucion}» Corregilo y reenvialo.
          </InlineAlert>
        )}

        <div className="adoc-det-cols">
          <div className="adoc-det-main">
            <ResumenPedido pedido={pedido} periodoNombre={periodoNombre} />

            {conSeguimiento && (
              <section className="adoc-det-hist" aria-label="Historial del pedido">
                <header className="adoc-hist-head">
                  <h2 className="adoc-hist-title">Historial del pedido</h2>
                  <span className="adoc-hist-note">Auditoría · Usuario · Fecha</span>
                </header>
                <AuditLog entries={historialAAuditEntries(pedido.historial)} />
              </section>
            )}
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

      <ModalConfirmarEliminar
        open={mostrarEliminar}
        onOpenChange={setMostrarEliminar}
        titulo="Eliminar pedido"
        objeto={
          <>
            el pedido {pedido?.numero ?? "sin número"} de{" "}
            <strong>"{pedido?.docente.nombre}"</strong>
          </>
        }
        error={errorEliminar}
        eliminando={eliminando}
        onConfirmar={onEliminar}
      />
    </>
  );
}
