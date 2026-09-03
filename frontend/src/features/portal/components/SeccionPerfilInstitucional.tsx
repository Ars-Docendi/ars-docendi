import { DataList, RoleBadge } from "@ars-docendi/ui";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { PerfilInstitucional } from "../types";
import "./portal.css";

interface SeccionPerfilInstitucionalProps {
  institucional: PerfilInstitucional;
}

/**
 * Identidad (Azure AD) y datos institucionales (Secretaría). Encabeza la página
 * con la persona, no con una tabla: es el ancla de todo el perfil.
 *
 * Se renderiza sin acción de encabezado: esa ausencia es lo que comunica que no
 * se edita. Nunca está vacío, así que el docente que entra por primera vez ya se
 * ve reconocido por el sistema.
 */
export function SeccionPerfilInstitucional({ institucional }: SeccionPerfilInstitucionalProps) {
  const usuario = useCurrentUser();

  return (
    <section className="portal-identidad">
      <h2 className="portal-seccion-titulo portal-oculto">Perfil</h2>
      <div className="portal-identidad-head">
        <RoleBadge
          name={`${institucional.apellido}, ${institucional.nombre}`}
          initials={usuario.initials}
          role={usuario.role}
        />
      </div>
      <div className="portal-identidad-datos">
        <DataList
          items={[
            { term: "Mail institucional", description: institucional.upn },
            { term: "DNI", description: institucional.documento },
            { term: "Legajo", description: institucional.legajo },
            { term: "CUIL", description: institucional.cuil },
          ]}
        />
      </div>
    </section>
  );
}
