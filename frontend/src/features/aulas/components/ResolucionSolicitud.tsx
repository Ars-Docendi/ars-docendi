import { useState } from "react";
import { Button, Field, InlineAlert, Input } from "@ars-docendi/ui";
import {
  useAsignarAula,
  useCancelarReserva,
  useCancelarSolicitud,
  useRechazarSolicitud,
} from "../hooks/useAccionesSolicitud";
import type { SolicitudReservaAula } from "../types";
import { ModalCancelarReserva } from "./ModalCancelarReserva";
import { ModalCancelarSolicitud } from "./ModalCancelarSolicitud";
import { ModalRechazarSolicitud } from "./ModalRechazarSolicitud";

const TEXTOS_ASIGNAR = {
  asignar: { boton: "Asignar y aprobar", errorTitulo: "No se pudo asignar el aula" },
  actualizar: { boton: "Actualizar aula", errorTitulo: "No se pudo actualizar el aula" },
} as const;

type ModalAbierto = "rechazar" | "cancelar-reserva" | "cancelar" | null;

interface ResolucionSolicitudProps {
  solicitud: SolicitudReservaAula;
  puedeAprobar: boolean;
}

/**
 * Panel de acciones del detalle. Qué se ofrece depende del permiso (la autoridad es el backend) y
 * del estado: quien resuelve asigna, actualiza, rechaza o cancela la reserva; el dueño cancela su
 * solicitud pendiente; una solicitud cerrada no admite más acciones.
 */
export function ResolucionSolicitud({ solicitud, puedeAprobar }: ResolucionSolicitudProps) {
  const rechazar = useRechazarSolicitud();
  const cancelarReserva = useCancelarReserva();
  const cancelar = useCancelarSolicitud();
  const [modal, setModal] = useState<ModalAbierto>(null);

  const resoluble =
    puedeAprobar && (solicitud.estado === "pendiente" || solicitud.estado === "aprobada");
  const cancelable = solicitud.esPropia && solicitud.estado === "pendiente";
  const infoCerrada =
    solicitud.estado === "aprobada"
      ? "Tu reserva está aprobada. Si necesitás cancelarla, pedíselo a Administración."
      : "Esta solicitud está cerrada: no admite más acciones.";

  function cerrarModal() {
    rechazar.reset();
    cancelarReserva.reset();
    cancelar.reset();
    setModal(null);
  }

  return (
    <>
      {resoluble && (
        <PanelResolver
          // Un cambio de aula o de estado (otra pestaña, otra acción) reinicia el campo.
          key={`${solicitud.estado}-${solicitud.aulaAsignada ?? ""}`}
          solicitud={solicitud}
          alRechazar={() => setModal("rechazar")}
          alCancelarReserva={() => setModal("cancelar-reserva")}
        />
      )}

      {cancelable && (
        <section className="adoc-aulas-card adoc-aulas-acc" aria-label="Acciones del docente">
          <h2 className="adoc-aulas-acc-title">Tu solicitud</h2>
          <Button
            id="detalle-cancelar"
            variant="destructive"
            className="adoc-aulas-bloque"
            onClick={() => setModal("cancelar")}
          >
            Cancelar solicitud
          </Button>
        </section>
      )}

      {!resoluble && !cancelable && (
        <InlineAlert severity="info" title="Sin acciones disponibles">
          {infoCerrada}
        </InlineAlert>
      )}

      {modal === "rechazar" && (
        <ModalRechazarSolicitud
          solicitud={solicitud}
          onCerrar={cerrarModal}
          error={rechazar.isError ? rechazar.error.message : undefined}
          rechazando={rechazar.isPending}
          onConfirmar={(motivo) =>
            rechazar.mutate({ id: solicitud.id, motivo }, { onSuccess: cerrarModal })
          }
        />
      )}
      {modal === "cancelar-reserva" && (
        <ModalCancelarReserva
          solicitud={solicitud}
          onCerrar={cerrarModal}
          error={cancelarReserva.isError ? cancelarReserva.error.message : undefined}
          cancelando={cancelarReserva.isPending}
          onConfirmar={(motivo) =>
            cancelarReserva.mutate({ id: solicitud.id, motivo }, { onSuccess: cerrarModal })
          }
        />
      )}
      {modal === "cancelar" && (
        <ModalCancelarSolicitud
          solicitud={solicitud}
          onCerrar={cerrarModal}
          error={cancelar.isError ? cancelar.error.message : undefined}
          cancelando={cancelar.isPending}
          onConfirmar={() => cancelar.mutate(solicitud.id, { onSuccess: cerrarModal })}
        />
      )}
    </>
  );
}

interface PanelResolverProps {
  solicitud: SolicitudReservaAula;
  alRechazar: () => void;
  alCancelarReserva: () => void;
}

function PanelResolver({ solicitud, alRechazar, alCancelarReserva }: PanelResolverProps) {
  const asignar = useAsignarAula();
  const [aula, setAula] = useState(solicitud.aulaAsignada ?? "");
  const [faltaAula, setFaltaAula] = useState(false);
  const textos = TEXTOS_ASIGNAR[solicitud.estado === "pendiente" ? "asignar" : "actualizar"];

  function enviarAula() {
    if (!aula.trim()) {
      setFaltaAula(true);
      return;
    }
    asignar.mutate({ id: solicitud.id, aulaAsignada: aula.trim() });
  }

  return (
    <section className="adoc-aulas-card adoc-aulas-acc" aria-label="Acciones sobre la solicitud">
      <h2 className="adoc-aulas-acc-title">Acciones</h2>
      {asignar.isError && (
        <InlineAlert severity="danger" title={textos.errorTitulo}>
          {asignar.error.message}
        </InlineAlert>
      )}
      <Field
        label="Aula o laboratorio"
        required
        error={faltaAula && !aula.trim() ? "Indicá el aula a asignar." : undefined}
      >
        <Input
          id="resolver-aula"
          value={aula}
          placeholder="Ej: Aula 204"
          onChange={(e) => setAula(e.target.value)}
        />
      </Field>
      <Button
        id="resolver-confirmar"
        variant="primary"
        className="adoc-aulas-bloque"
        loading={asignar.isPending}
        onClick={enviarAula}
      >
        {textos.boton}
      </Button>
      {solicitud.estado === "pendiente" ? (
        <Button
          id="resolver-rechazar"
          variant="destructive"
          className="adoc-aulas-bloque"
          disabled={asignar.isPending}
          onClick={alRechazar}
        >
          Rechazar solicitud
        </Button>
      ) : (
        <Button
          id="resolver-cancelar-reserva"
          variant="destructive"
          className="adoc-aulas-bloque"
          disabled={asignar.isPending}
          onClick={alCancelarReserva}
        >
          Cancelar reserva
        </Button>
      )}
    </section>
  );
}
