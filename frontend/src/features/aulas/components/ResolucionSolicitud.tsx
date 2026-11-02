import { useState } from "react";
import { Button, Field, InlineAlert, Select } from "@ars-docendi/ui";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { useGuardiaDeCambios } from "../../../shared/navegacion/guardias";
import {
  useAsignarAula,
  useCancelarReserva,
  useCancelarSolicitud,
  useRechazarSolicitud,
} from "../hooks/useAccionesSolicitud";
import { useAulasActivas } from "../hooks/useCatalogoAulas";
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
          key={`${solicitud.estado}-${solicitud.aulaId ?? ""}`}
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
  const aulas = useAulasActivas();
  const [aulaId, setAulaId] = useState(solicitud.aulaId ?? "");
  const [faltaAula, setFaltaAula] = useState(false);
  const textos = TEXTOS_ASIGNAR[solicitud.estado === "pendiente" ? "asignar" : "actualizar"];
  // El aula elegida y sin confirmar se pierde al salir de la página: se avisa.
  useGuardiaDeCambios(aulaId !== (solicitud.aulaId ?? ""));
  const elegida = aulas.data?.find((aula) => aula.id === aulaId);
  const sinAulas = aulas.isSuccess && aulas.data.length === 0;

  function enviarAula() {
    if (!aulaId) {
      setFaltaAula(true);
      return;
    }
    asignar.mutate({ id: solicitud.id, aulaId });
  }

  return (
    <section className="adoc-aulas-card adoc-aulas-acc" aria-label="Acciones sobre la solicitud">
      <h2 className="adoc-aulas-acc-title">Acciones</h2>
      {asignar.isError && (
        <InlineAlert severity="danger" title={textos.errorTitulo}>
          {mensajeProblema(asignar.error, "Intentá de nuevo.")}
        </InlineAlert>
      )}
      {aulas.isError && (
        <InlineAlert severity="danger" title="No se pudo cargar el catálogo de aulas">
          {mensajeProblema(aulas.error, "Recargá la página para reintentar.")}
        </InlineAlert>
      )}
      <Field
        label="Aula o laboratorio"
        required
        error={
          sinAulas
            ? "No hay aulas activas en el catálogo."
            : faltaAula && !aulaId
              ? "Elegí el aula a asignar."
              : undefined
        }
      >
        <Select
          id="resolver-aula"
          value={aulaId}
          disabled={!aulas.isSuccess || sinAulas}
          onChange={(e) => setAulaId(e.target.value)}
        >
          <option value="">{aulas.isLoading ? "Cargando aulas…" : "Seleccioná un aula…"}</option>
          {aulas.data?.map((aula) => (
            <option key={aula.id} value={aula.id}>
              {`${aula.nombre} · ${aula.tipo === "laboratorio" ? "Laboratorio" : "Aula"} · ${aula.capacidad} lugares`}
            </option>
          ))}
        </Select>
      </Field>
      {/* BR-aulas-007: la cantidad es aproximada; se advierte y se permite asignar igual. */}
      {elegida && solicitud.cantidadAlumnosAprox > elegida.capacidad && (
        <InlineAlert severity="warning" title="Capacidad insuficiente">
          {`${elegida.nombre} tiene ${elegida.capacidad} lugares y la solicitud indica ${solicitud.cantidadAlumnosAprox} alumnos. Podés asignarla igual.`}
        </InlineAlert>
      )}
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
