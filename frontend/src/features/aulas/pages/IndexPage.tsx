import { useMemo, useState } from "react";
import { Button, InlineAlert } from "@ars-docendi/ui";
import { Migas } from "../../../shared/navegacion/Migas";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { useFiltrosURL, useOrdenURL } from "../../../shared/navegacion/estadoURL";
import { useAbrirDetalle } from "../../../shared/navegacion/useEstadoVolver";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { AlertaConReintento } from "../components/AlertaConReintento";
import { TablaMisSolicitudes } from "../components/TablaMisSolicitudes";
import { TablaTodasLasSolicitudes } from "../components/TablaTodasLasSolicitudes";
import { ModalNuevaSolicitud } from "../components/ModalNuevaSolicitud";
import { ModalCancelarSolicitud } from "../components/ModalCancelarSolicitud";
import { IconoPlus } from "../components/lucide";
import {
  aplicarFiltrosYOrdenSolicitudes,
  COLUMNAS_ORDEN_SOLICITUDES,
  FILTROS_INICIALES,
} from "../components/filtrosSolicitudes";
import { useMisSolicitudes, useTodasLasSolicitudes } from "../hooks/useSolicitudesAula";
import { useCancelarSolicitud } from "../hooks/useAccionesSolicitud";
import { rutaDetalleSolicitud } from "../rutas";
import type { SolicitudReservaAula } from "../types";
import "./reservaAulas.css";

export function IndexPage() {
  const { user } = useCurrentUser();
  const puedeSolicitar = Boolean(user?.permissions.includes("aulas.solicitar"));
  const puedeAprobar = Boolean(user?.permissions.includes("aulas.aprobar"));

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Reserva de aulas" }]} />
      <PageHeader title="Reserva de aulas" />

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
  const abrir = useAbrirDetalle();
  const { data: solicitudes, isLoading, isError, refetch } = useMisSolicitudes();
  const cancelar = useCancelarSolicitud();

  // Dos tablas en la página: el query de cada una lleva su prefijo.
  const [filtros, setFiltros] = useFiltrosURL(FILTROS_INICIALES, "mias");
  const [orden, setOrden] = useOrdenURL(COLUMNAS_ORDEN_SOLICITUDES, "mias");
  const [modalNuevaAbierto, setModalNuevaAbierto] = useState(false);
  const [solicitudACancelar, setSolicitudACancelar] = useState<SolicitudReservaAula | undefined>();

  const filtradas = useMemo(
    () => aplicarFiltrosYOrdenSolicitudes(solicitudes ?? [], filtros, orden),
    [solicitudes, filtros, orden],
  );

  const total = solicitudes?.length ?? 0;

  function cerrarCancelar() {
    cancelar.reset();
    setSolicitudACancelar(undefined);
  }

  return (
    <section className="adoc-aulas-section" aria-label="Mis solicitudes">
      <div className="adoc-aulas-section-header">
        <h2>Mis solicitudes</h2>
        <Button
          id="nueva-solicitud"
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
        <AlertaConReintento
          id="mias-reintentar"
          titulo="No se pudieron cargar tus solicitudes"
          onReintentar={() => void refetch()}
        >
          Hubo un problema al obtener tus solicitudes.
        </AlertaConReintento>
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
            onAbrir={(solicitud) => abrir(rutaDetalleSolicitud(solicitud.id))}
          />
        </>
      )}

      {modalNuevaAbierto && <ModalNuevaSolicitud onCerrar={() => setModalNuevaAbierto(false)} />}

      {solicitudACancelar && (
        <ModalCancelarSolicitud
          solicitud={solicitudACancelar}
          onCerrar={cerrarCancelar}
          error={cancelar.isError ? cancelar.error.message : undefined}
          cancelando={cancelar.isPending}
          onConfirmar={() => cancelar.mutate(solicitudACancelar.id, { onSuccess: cerrarCancelar })}
        />
      )}
    </section>
  );
}

function SeccionTodasLasSolicitudes() {
  const abrir = useAbrirDetalle();
  const { data: solicitudes, isLoading, isError, refetch } = useTodasLasSolicitudes();

  const [filtros, setFiltros] = useFiltrosURL(FILTROS_INICIALES, "todas");
  const [orden, setOrden] = useOrdenURL(COLUMNAS_ORDEN_SOLICITUDES, "todas");

  const filtradas = useMemo(
    () => aplicarFiltrosYOrdenSolicitudes(solicitudes ?? [], filtros, orden),
    [solicitudes, filtros, orden],
  );

  const total = solicitudes?.length ?? 0;

  return (
    <section className="adoc-aulas-section" aria-label="Todas las solicitudes">
      <div className="adoc-aulas-section-header">
        <h2>Todas las solicitudes</h2>
      </div>

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando solicitudes…</p>}

      {isError && (
        <AlertaConReintento
          id="todas-reintentar"
          titulo="No se pudieron cargar las solicitudes"
          onReintentar={() => void refetch()}
        >
          Hubo un problema al obtener las solicitudes.
        </AlertaConReintento>
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
            onAbrir={(solicitud) => abrir(rutaDetalleSolicitud(solicitud.id))}
          />
        </>
      )}
    </section>
  );
}
