import { useMemo, useState } from "react";
import { InlineAlert } from "@ars-docendi/ui";

import { mensajeProblema } from "../../../shared/api/problemDetails";
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
  const [errorDescarga, setErrorDescarga] = useState<string | null>(null);

  const modo: ModoPerfil = useMemo(() => {
    const descargar = (pedido: Promise<Blob>, nombre: string) => {
      setErrorDescarga(null);
      pedido
        .then((blob) => guardarArchivo(blob, nombre))
        .catch((e: unknown) =>
          setErrorDescarga(mensajeProblema(e, "No se pudo descargar el archivo.")),
        );
    };
    return {
      soloLectura: !editable,
      descargarCv: () => descargar(api.descargarCv(), perfil?.cv?.nombre ?? "cv.pdf"),
      descargarDocumento: (proyectoId) =>
        descargar(
          api.descargarDocumento(proyectoId),
          perfil?.proyectos.find((p) => p.id === proyectoId)?.documento?.nombre ?? "documento.pdf",
        ),
    };
  }, [api, editable, perfil]);

  if (estado === "cargando") return <p role="status">Cargando el perfil…</p>;
  if (estado === "error" || !perfil) {
    return <InlineAlert severity="danger" title="No se pudo cargar el perfil de Mi Portal." />;
  }
  return (
    <ModoPerfilContexto.Provider value={modo}>
      {error && <InlineAlert severity="danger" title={error} />}
      {errorDescarga && <InlineAlert severity="danger" title={errorDescarga} />}
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
