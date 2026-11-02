import { Link, useParams } from "react-router-dom";
import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { DatosSolicitud } from "../components/DatosSolicitud";
import { fechaSolicitud } from "../components/filtrosSolicitudes";
import { IconoArrowLeft } from "../components/lucide";
import { ResolucionSolicitud } from "../components/ResolucionSolicitud";
import { useSolicitud } from "../hooks/useSolicitudesAula";
import "./detalleSolicitud.css";

const RUTA_LISTADO = "/aulas";
const ETIQUETA_LISTADO = "Reserva de aulas";

/**
 * Detalle de una solicitud de reserva de aula: la ve su dueño o quien resuelve solicitudes. Acá se
 * resuelve (asignar, rechazar, cancelar la reserva) o se cancela la propia pendiente.
 */
export function DetalleSolicitudPage() {
  const { id } = useParams();
  const { user } = useCurrentUser();
  const { data: solicitud, isError } = useSolicitud(id);
  const puedeAprobar = Boolean(user?.permissions.includes("aulas.aprobar"));

  const volver = (
    <Link
      id="solicitud-volver"
      to={RUTA_LISTADO}
      className="adoc-btn v-secondary adoc-aulas-volver"
    >
      <span className="adoc-ico">
        <IconoArrowLeft />
      </span>
      Volver a {ETIQUETA_LISTADO}
    </Link>
  );

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: ETIQUETA_LISTADO, href: RUTA_LISTADO },
          { label: "Solicitud" },
        ]}
      />

      {isError && (
        <>
          <PageHeader title="Solicitud" actions={volver} />
          <InlineAlert severity="danger" title="No se pudo cargar la solicitud">
            La solicitud no existe o no tenés acceso.
          </InlineAlert>
        </>
      )}

      {!isError && !solicitud && (
        <>
          <PageHeader title="Solicitud" actions={volver} />
          <p role="status" style={{ color: "var(--color-text-secondary)" }}>
            Cargando la solicitud…
          </p>
        </>
      )}

      {solicitud && (
        <>
          <PageHeader
            title={`Solicitud del ${fechaSolicitud(solicitud)}`}
            meta={`${solicitud.materia.nombre} · ${solicitud.comision}`}
            actions={volver}
          />
          <div className="adoc-aulas-detalle">
            <div className="adoc-aulas-detalle-col">
              <DatosSolicitud solicitud={solicitud} />
            </div>
            <div className="adoc-aulas-detalle-col">
              <ResolucionSolicitud solicitud={solicitud} puedeAprobar={puedeAprobar} />
            </div>
          </div>
        </>
      )}
    </>
  );
}
