import { Button } from "@ars-docendi/ui";
import type { PermisoRol, RolMock } from "../models";

interface PanelPermisosProps {
  rol: RolMock;
  permisos: PermisoRol[];
  asignados: string[];
  puedeEditar: boolean;
  guardando: boolean;
  onToggle: (permisoId: string) => void;
  onGuardar: () => void;
}

export function PanelPermisos({
  rol,
  permisos,
  asignados,
  puedeEditar,
  guardando,
  onToggle,
  onGuardar,
}: PanelPermisosProps) {
  return (
    <section aria-label={`Permisos de ${rol.nombre}`} className="roles-permissions-panel">
      <header>
        <div>
          <h2>{rol.nombre}</h2>
          <p>{rol.es_sistema ? "Rol de sistema" : "Rol personalizado"}</p>
          <small>
            Código: {rol.codigo} · Versión: {rol.version}
          </small>
        </div>
        <span>
          {asignados.length} de {permisos.length}
        </span>
      </header>
      {permisos.map((permiso) => (
        <label key={permiso.id}>
          <input
            type="checkbox"
            checked={asignados.includes(permiso.id)}
            disabled={!puedeEditar || guardando}
            onChange={() => onToggle(permiso.id)}
          />
          <span>
            <strong>{permiso.nombre}</strong>
            <small>{permiso.descripcion}</small>
          </span>
        </label>
      ))}
      <footer>
        <span>{puedeEditar ? "Los cambios se aplican al guardar." : "Sólo lectura."}</span>
        {puedeEditar && (
          <Button variant="primary" size="sm" disabled={guardando} onClick={onGuardar}>
            {guardando ? "Guardando…" : "Guardar cambios"}
          </Button>
        )}
      </footer>
    </section>
  );
}
