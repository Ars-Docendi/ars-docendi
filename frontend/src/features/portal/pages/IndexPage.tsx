import { Breadcrumbs, InlineAlert } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { usePerfilDocente } from "../hooks/usePerfilDocente";
import { agregarTag, quitarTag } from "../helpers";
import { AvisoGuardado } from "../components/AvisoGuardado";
import { SeccionCertificaciones } from "../components/SeccionCertificaciones";
import { SeccionContacto } from "../components/SeccionContacto";
import { SeccionCv } from "../components/SeccionCv";
import { SeccionEducacion } from "../components/SeccionEducacion";
import { SeccionExperiencia } from "../components/SeccionExperiencia";
import { SeccionPerfilInstitucional } from "../components/SeccionPerfilInstitucional";
import { SeccionProyectos } from "../components/SeccionProyectos";
import { SeccionTags } from "../components/SeccionTags";
import "../components/portal.css";

/**
 * "Mi Portal" — el perfil del docente autenticado.
 *
 * Es un perfil vivo, no un formulario: se lee por defecto y se edita por
 * sección, cada una con su propio guardado. No hay guardado global ni campos
 * obligatorios. Las secciones sin datos ocupan una fila y se expanden al
 * llenarse, así que la página crece con el perfil.
 */
export function IndexPage() {
  const { estado, perfil, guardado, error, actualizar, ocultarAviso } = usePerfilDocente();

  const encabezado = (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Mi Portal" }]} />
      <PageHeader title="Mi Portal" />
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
      <div className="portal-secciones">
        <SeccionPerfilInstitucional institucional={perfil.institucional} />

        <SeccionContacto
          contacto={perfil.contacto}
          onGuardar={(contacto) => actualizar((p) => ({ ...p, contacto }))}
        />

        <SeccionCv
          cv={perfil.cv}
          onCargar={(cv) => actualizar((p) => ({ ...p, cv }))}
          onEliminar={() => actualizar((p) => ({ ...p, cv: null }))}
        />

        <SeccionExperiencia
          items={perfil.experiencia}
          onCambio={(experiencia) => actualizar((p) => ({ ...p, experiencia }))}
        />

        <SeccionEducacion
          items={perfil.educacion}
          onCambio={(educacion) => actualizar((p) => ({ ...p, educacion }))}
        />

        <SeccionCertificaciones
          items={perfil.certificaciones}
          onCambio={(certificaciones) => actualizar((p) => ({ ...p, certificaciones }))}
        />

        <SeccionProyectos
          items={perfil.proyectos}
          onCambio={(proyectos) => actualizar((p) => ({ ...p, proyectos }))}
        />

        <SeccionTags
          titulo="Habilidades"
          tags={perfil.habilidades}
          onAgregar={(t) =>
            actualizar((p) => ({ ...p, habilidades: agregarTag(p.habilidades, t) }))
          }
          onQuitar={(t) => actualizar((p) => ({ ...p, habilidades: quitarTag(p.habilidades, t) }))}
        />

        <SeccionTags
          titulo="Intereses"
          tags={perfil.intereses}
          onAgregar={(t) => actualizar((p) => ({ ...p, intereses: agregarTag(p.intereses, t) }))}
          onQuitar={(t) => actualizar((p) => ({ ...p, intereses: quitarTag(p.intereses, t) }))}
        />
      </div>

      <AvisoGuardado visible={guardado} onCerrar={ocultarAviso} />
    </>
  );
}
