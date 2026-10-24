import { agregarTag, quitarTag } from "../helpers";
import type { PerfilDocente } from "../types";
import { SeccionCertificaciones } from "./SeccionCertificaciones";
import { SeccionContacto } from "./SeccionContacto";
import { SeccionCv } from "./SeccionCv";
import { SeccionEducacion } from "./SeccionEducacion";
import { SeccionExperiencia } from "./SeccionExperiencia";
import { SeccionPerfilInstitucional } from "./SeccionPerfilInstitucional";
import { SeccionProyectos } from "./SeccionProyectos";
import { SeccionTags } from "./SeccionTags";
import "./portal.css";

interface PerfilSeccionesProps {
  perfil: PerfilDocente;
  actualizar: (cambio: (perfil: PerfilDocente) => PerfilDocente) => void;
  cargarCv: (archivo: File) => void;
  borrarCv: () => void;
  /** El bloque Perfil (datos institucionales); en la administración ya están en Datos personales. */
  conInstitucional?: boolean;
}

/** Las secciones de Mi Portal, en su orden. El modo (editable o sólo lectura) lo da `ModoPerfil`. */
export function PerfilSecciones({
  perfil,
  actualizar,
  cargarCv,
  borrarCv,
  conInstitucional = true,
}: PerfilSeccionesProps) {
  return (
    <div className="portal-secciones">
      {conInstitucional && <SeccionPerfilInstitucional institucional={perfil.institucional} />}

      <SeccionContacto
        contacto={perfil.contacto}
        onGuardar={(contacto) => actualizar((p) => ({ ...p, contacto }))}
      />

      <SeccionCv cv={perfil.cv} onCargar={cargarCv} onEliminar={borrarCv} />

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
        onAgregar={(t) => actualizar((p) => ({ ...p, habilidades: agregarTag(p.habilidades, t) }))}
        onQuitar={(t) => actualizar((p) => ({ ...p, habilidades: quitarTag(p.habilidades, t) }))}
      />

      <SeccionTags
        titulo="Intereses"
        tags={perfil.intereses}
        onAgregar={(t) => actualizar((p) => ({ ...p, intereses: agregarTag(p.intereses, t) }))}
        onQuitar={(t) => actualizar((p) => ({ ...p, intereses: quitarTag(p.intereses, t) }))}
      />
    </div>
  );
}
