import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { IconoHash, IconoIdCard, IconoMail, IconoUser } from "../../../shared/ui/iconos";
import type { PerfilInstitucional } from "../types";
import { FilaDato } from "./FilaDato";
import "./portal.css";

interface SeccionPerfilInstitucionalProps {
  institucional: PerfilInstitucional;
}

/**
 * Identidad (Azure AD) y datos institucionales (Secretaría). Encabeza la página
 * con la persona, no con una tabla: es el ancla de todo el perfil.
 *
 * No usa `RoleBadge` a propósito: ese componente dibuja siempre un caret —que
 * solo es accionable con `multi`— y acá no hay nada que desplegar. Además ya
 * está en la topbar, así que repetirlo mostraba lo mismo dos veces.
 *
 * Ninguna fila lleva chevron: no hay nada que abrir. Esa ausencia es lo que
 * comunica que estos datos no se editan acá, sin una línea de texto que lo
 * explique.
 */
export function SeccionPerfilInstitucional({ institucional }: SeccionPerfilInstitucionalProps) {
  const usuario = useCurrentUser();

  return (
    <section className="portal-identidad">
      {/* Nombre accesible de la sección; el titular visible es la persona. */}
      <h2 className="portal-seccion-titulo portal-oculto">Perfil</h2>
      <div className="portal-identidad-head">
        <span className="portal-avatar" aria-hidden="true">
          {usuario.user?.initials ?? ""}
        </span>
        <span className="portal-identidad-nombre">
          <p className="portal-identidad-titulo">
            {institucional.apellido}, {institucional.nombre}
          </p>
          <span className="portal-identidad-rol">{usuario.user?.role ?? ""}</span>
        </span>
      </div>
      <div className="portal-filas">
        <FilaDato icono={<IconoMail />} valor={institucional.upn} etiqueta="Mail institucional" />
        <FilaDato
          icono={<IconoIdCard />}
          valor={institucional.documento}
          etiqueta="Número de DNI"
        />
        <FilaDato icono={<IconoHash />} valor={institucional.legajo} etiqueta="Legajo" />
        <FilaDato icono={<IconoUser />} valor={institucional.cuil} etiqueta="CUIL" />
      </div>
    </section>
  );
}
