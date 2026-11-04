import { StatusBadge } from "@ars-docendi/ui";

import { ETIQUETAS_ESTADO, estadoDe } from "../filtrosRoles";
import type { RolMock } from "../models";

/** Active/inactive badge shared by the table and the phone cards. */
export function InsigniaEstadoRol({ rol }: { rol: RolMock }) {
  return (
    <StatusBadge
      kind={rol.activo ? "aprobado" : "rechazado"}
      label={ETIQUETAS_ESTADO[estadoDe(rol)]}
    />
  );
}
