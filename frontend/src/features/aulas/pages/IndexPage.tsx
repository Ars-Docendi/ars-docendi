import { useMemo, useState } from "react";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { TablaMisSolicitudes } from "../components/TablaMisSolicitudes";
import { TablaTodasLasSolicitudes } from "../components/TablaTodasLasSolicitudes";
import { ModalNuevaSolicitud } from "../components/ModalNuevaSolicitud";
import { ModalCancelarSolicitud } from "../components/ModalCancelarSolicitud";
import { ModalAsignarAula, type ModoAsignarAula } from "../components/ModalAsignarAula";
import { ModalRechazarSolicitud } from "../components/ModalRechazarSolicitud";
import { ModalDetalleSolicitud } from "../components/ModalDetalleSolicitud";
import { IconoPlus } from "../components/lucide";
import {
  aplicarFiltrosYOrdenSolicitudes,
  FILTROS_INICIALES,
  type FiltrosSolicitudesState,
  type OrdenSolicitudes,
} from "../components/filtrosSolicitudes";
import { useMisSolicitudes, useTodasLasSolicitudes } from "../hooks/useSolicitudesAula";
import {
  useAsignarAula,
  useCancelarSolicitud,
  useCrearSolicitud,
  useRechazarSolicitud,
} from "../hooks/useAccionesSolicitud";
import type { SolicitudReservaAula } from "../types";
import "./reservaAulas.css";

export function IndexPage() {
  const { user } = useCurrentUser();
  const puedeSolicitar = Boolean(user?.permissions.includes("aulas.solicitar"));
  const puedeAprobar = Boolean(user?.permissions.includes("aulas.aprobar"));

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Aulas" }]} />
      <PageHeader pretitle="Reserva de Aulas" title="Reserva de Aulas" />

      {puedeSolicitar && <SeccionMisSolicitudes />}
      {puedeAprobar && <SeccionTodasLasSolicitudes />}
      {!puedeSolicitar && !puedeAprobar && (
        <InlineAlert severity="info" title="Sin acceso">
          Tu rol no tiene permisos para operar en Reserva de Aulas.
        </InlineAlert>
      )}
    </>
  );
}

function SeccionMisSolicitudes() {
  const { data: solicitudes, isLoading, isError, refetch } = useMisSolicitudes();
  const crear = useCrearSolicitud();
  const cancelar = useCancelarSolicitud();

  const [filtros, setFiltros] = useState<FiltrosSolicitudesState>(FILTROS_INICIALES);
  const [orden, setOrden] = useState<OrdenSolicitudes | null>(null);
  const [modalNuevaAbierto, setModalNuevaAbierto] = useState(false);
  const [solicitudACancelar, setSolicitudACancelar] = useState<SolicitudReservaAula | undefined>();
  const [solicitudDetalle, setSolicitudDetalle] = useState<SolicitudReservaAula | undefined>();

  const filtradas = useMemo(
    () => aplicarFiltrosYOrdenSolicitudes(solicitudes ?? [], filtros, orden),
    [solicitudes, filtros, orden],
  );

  const total = solicitudes?.length ?? 0;

  function handleConfirmarCancelar() {
    if (!solicitudACancelar) return;
    cancelar.mutate(solicitudACancelar.id, {
      onSuccess: () => setSolicitudACancelar(undefined),
    });
  }

  return (
    <section className="adoc-aulas-section">
      <div className="adoc-aulas-section-header">
        <h2>Mis solicitudes</h2>
        <Button
          variant="primary"
          leadingIcon={<IconoPlus />}
          onClick={() => setModalNuevaAbierto(true)}
        >
          Nueva solicitud
        </Button>
      </div>

      {isLoading && (
        <p style={{ color: "var(--color-text-secondary)" }}>Cargando tus solicitudes…</p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar tus solicitudes">
          Hubo un problema al obtener tus solicitudes.{" "}
          <button onClick={() => refetch()}>Reintentar</button>.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no generaste solicitudes">
          Empezá creando tu primera solicitud de reserva de aula con "Nueva solicitud".
        </InlineAlert>
      )}

      {!isLoading && !isError && total > 0 && (
        <>
          {filtradas.length === 0 && (
            <InlineAlert severity="info" title="Sin resultados">
              Ninguna solicitud coincide con la búsqueda o el filtro aplicado.
            </InlineAlert>
          )}
          <TablaMisSolicitudes
            solicitudes={filtradas}
            solicitudesParaOpciones={solicitudes}
            filtros={filtros}
            orden={orden}
            onFiltrosChange={setFiltros}
            onOrdenChange={setOrden}
            onCancelar={setSolicitudACancelar}
            onVerDetalle={setSolicitudDetalle}
          />
        </>
      )}

      <ModalNuevaSolicitud
        open={modalNuevaAbierto}
        onOpenChange={setModalNuevaAbierto}
        error={crear.isError ? crear.error.message : undefined}
        guardando={crear.isPending}
        onGuardar={(datos) => crear.mutate(datos, { onSuccess: () => setModalNuevaAbierto(false) })}
      />

      <ModalCancelarSolicitud
        open={solicitudACancelar !== undefined}
        onOpenChange={(open) => {
          if (!open) setSolicitudACancelar(undefined);
        }}
        solicitud={solicitudACancelar}
        error={cancelar.isError ? cancelar.error.message : undefined}
        cancelando={cancelar.isPending}
        onConfirmar={handleConfirmarCancelar}
      />

      <ModalDetalleSolicitud
        open={solicitudDetalle !== undefined}
        onOpenChange={(open) => {
          if (!open) setSolicitudDetalle(undefined);
        }}
        solicitud={solicitudDetalle}
      />
    </section>
  );
}

function SeccionTodasLasSolicitudes() {
  const { data: solicitudes, isLoading, isError, refetch } = useTodasLasSolicitudes();
  const asignar = useAsignarAula();
  const rechazar = useRechazarSolicitud();

  const [filtros, setFiltros] = useState<FiltrosSolicitudesState>(FILTROS_INICIALES);
  const [orden, setOrden] = useState<OrdenSolicitudes | null>(null);
  const [edicionAula, setEdicionAula] = useState<
    { solicitud: SolicitudReservaAula; modo: ModoAsignarAula } | undefined
  >();
  const [solicitudARechazar, setSolicitudARechazar] = useState<SolicitudReservaAula | undefined>();

  const filtradas = useMemo(
    () => aplicarFiltrosYOrdenSolicitudes(solicitudes ?? [], filtros, orden),
    [solicitudes, filtros, orden],
  );

  const total = solicitudes?.length ?? 0;

  function handleConfirmarAsignar(aulaAsignada: string) {
    if (!edicionAula) return;
    asignar.mutate(
      { id: edicionAula.solicitud.id, aulaAsignada },
      { onSuccess: () => setEdicionAula(undefined) },
    );
  }

  function handleConfirmarRechazar(motivo: string) {
    if (!solicitudARechazar) return;
    rechazar.mutate(
      { id: solicitudARechazar.id, motivo },
      { onSuccess: () => setSolicitudARechazar(undefined) },
    );
  }

  return (
    <section className="adoc-aulas-section">
      <div className="adoc-aulas-section-header">
        <h2>Todas las solicitudes</h2>
      </div>

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando solicitudes…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar las solicitudes">
          Hubo un problema al obtener las solicitudes.{" "}
          <button onClick={() => refetch()}>Reintentar</button>.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no hay solicitudes">
          Cuando un docente genere una solicitud de reserva de aula, va a aparecer acá.
        </InlineAlert>
      )}

      {!isLoading && !isError && total > 0 && (
        <>
          {filtradas.length === 0 && (
            <InlineAlert severity="info" title="Sin resultados">
              Ninguna solicitud coincide con la búsqueda o el filtro aplicado.
            </InlineAlert>
          )}
          <TablaTodasLasSolicitudes
            solicitudes={filtradas}
            solicitudesParaOpciones={solicitudes}
            filtros={filtros}
            orden={orden}
            onFiltrosChange={setFiltros}
            onOrdenChange={setOrden}
            onAsignarAula={(solicitud) => setEdicionAula({ solicitud, modo: "asignar" })}
            onActualizarAula={(solicitud) => setEdicionAula({ solicitud, modo: "actualizar" })}
          />
        </>
      )}

      <ModalAsignarAula
        key={edicionAula ? `${edicionAula.solicitud.id}-${edicionAula.modo}` : "asignar-cerrado"}
        open={edicionAula !== undefined}
        modo={edicionAula?.modo ?? "asignar"}
        onOpenChange={(open) => {
          if (!open) setEdicionAula(undefined);
        }}
        solicitud={edicionAula?.solicitud}
        error={asignar.isError ? asignar.error.message : undefined}
        asignando={asignar.isPending}
        onConfirmar={handleConfirmarAsignar}
        onRechazar={(solicitud) => {
          setEdicionAula(undefined);
          setSolicitudARechazar(solicitud);
        }}
      />

      <ModalRechazarSolicitud
        key={solicitudARechazar?.id ?? "rechazar-cerrado"}
        open={solicitudARechazar !== undefined}
        onOpenChange={(open) => {
          if (!open) setSolicitudARechazar(undefined);
        }}
        solicitud={solicitudARechazar}
        error={rechazar.isError ? rechazar.error.message : undefined}
        rechazando={rechazar.isPending}
        onConfirmar={handleConfirmarRechazar}
      />
    </section>
  );
}
