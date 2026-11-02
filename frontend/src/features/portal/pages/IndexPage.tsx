import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { usePerfilDocente } from "../../../shared/perfil/hooks/usePerfilDocente";
import { AvisoGuardado } from "../../../shared/perfil/components/AvisoGuardado";
import { PerfilSecciones } from "../../../shared/perfil/components/PerfilSecciones";
import "../../../shared/perfil/components/portal.css";

/**
 * "Mi Portal" — el perfil del docente autenticado.
 *
 * Es un perfil vivo, no un formulario: se lee por defecto y se edita por
 * sección, cada una con su propio guardado. No hay guardado global ni campos
 * obligatorios. Las secciones sin datos ocupan una fila y se expanden al
 * llenarse, así que la página crece con el perfil.
 */
export function IndexPage() {
  const { estado, perfil, guardado, error, actualizar, cargarCv, borrarCv, ocultarAviso } =
    usePerfilDocente();

  const encabezado = (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Mi portal" }]} />
      <PageHeader title="Mi portal" />
    </>
  );

  if (estado === "cargando") {
    return (
      <>
        {encabezado}
        <p>Cargando tu perfil…</p>
      </>
    );
  }

  if (estado === "error" || !perfil) {
    return (
      <>
        {encabezado}
        <InlineAlert severity="danger" title="No pudimos cargar tu perfil">
          Reintentá en unos minutos o escribí a Secretaría Académica si el problema persiste.
        </InlineAlert>
      </>
    );
  }

  return (
    <>
      {encabezado}
      {error && (
        <InlineAlert severity="danger" title="No pudimos guardar los cambios">
          {error}
        </InlineAlert>
      )}
      <PerfilSecciones
        perfil={perfil}
        actualizar={actualizar}
        cargarCv={(archivo) => void cargarCv(archivo)}
        borrarCv={() => void borrarCv()}
      />

      <AvisoGuardado visible={guardado} onCerrar={ocultarAviso} />
    </>
  );
}
