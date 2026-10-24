import { useMemo } from "react";
import { InlineAlert } from "@ars-docendi/ui";

import { PerfilSecciones } from "../../../shared/perfil/components/PerfilSecciones";
import { ModoPerfilContexto, type ModoPerfil } from "../../../shared/perfil/components/ModoPerfil";
import { usePerfil } from "../../../shared/perfil/hooks/usePerfilDocente";
import { apiPortalDePersona, personasKeys } from "../api/personasApi";

function guardarArchivo(blob: Blob, nombre: string) {
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = nombre;
  enlace.click();
  URL.revokeObjectURL(url);
}

/**
 * Perfil de Mi Portal de la persona con la misma estructura que la pantalla del docente.
 * Editable con `portal.administrar` (cada cambio se guarda al momento); si no, sólo lectura con
 * descargas.
 */
export function PortalPersona({ personaId, editable }: { personaId: string; editable: boolean }) {
  const api = useMemo(() => apiPortalDePersona(personaId), [personaId]);
  const clave = useMemo(() => personasKeys.portal(personaId), [personaId]);
  const { estado, perfil, error, actualizar, cargarCv, borrarCv } = usePerfil(api, clave);

  const modo: ModoPerfil = useMemo(
    () => ({
      soloLectura: !editable,
      descargarCv: () =>
        void api.descargarCv().then((blob) => guardarArchivo(blob, perfil?.cv?.nombre ?? "cv.pdf")),
      descargarDocumento: (proyectoId) =>
        void api
          .descargarDocumento(proyectoId)
          .then((blob) =>
            guardarArchivo(
              blob,
              perfil?.proyectos.find((p) => p.id === proyectoId)?.documento?.nombre ??
                "documento.pdf",
            ),
          ),
    }),
    [api, editable, perfil],
  );

  if (estado === "cargando") return <p role="status">Cargando el perfil…</p>;
  if (estado === "error" || !perfil) {
    return <InlineAlert severity="danger" title="No se pudo cargar el perfil de Mi Portal." />;
  }
  return (
    <ModoPerfilContexto.Provider value={modo}>
      {error && <InlineAlert severity="danger" title={error} />}
      <PerfilSecciones
        perfil={perfil}
        actualizar={actualizar}
        cargarCv={(archivo) => void cargarCv(archivo)}
        borrarCv={() => void borrarCv()}
        conInstitucional={false}
      />
    </ModoPerfilContexto.Provider>
  );
}
