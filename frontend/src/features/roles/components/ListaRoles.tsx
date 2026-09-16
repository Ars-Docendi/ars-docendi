import type { RolMock } from "../models";

interface ListaRolesProps {
  roles: RolMock[];
  busqueda: string;
  onBusquedaChange: (valor: string) => void;
  rolSeleccionadoId: string | null;
  onSeleccionar: (rol: RolMock) => void;
  puedeAdministrar: boolean;
  onEditar: (rol: RolMock) => void;
  onEliminar: (rol: RolMock) => void;
}

export function ListaRoles({
  roles,
  busqueda,
  onBusquedaChange,
  rolSeleccionadoId,
  onSeleccionar,
  puedeAdministrar,
  onEditar,
  onEliminar,
}: ListaRolesProps) {
  return (
    <section aria-label="Roles activos" className="roles-list-panel">
      <label>
        Buscar rol
        <input
          type="search"
          placeholder="Nombre o descripción…"
          value={busqueda}
          onChange={(e) => onBusquedaChange(e.target.value)}
        />
      </label>
      <ul>
        {roles.map((rol) => (
          <li key={rol.id}>
            <button
              type="button"
              className={rol.id === rolSeleccionadoId ? "selected" : undefined}
              aria-pressed={rol.id === rolSeleccionadoId}
              onClick={() => onSeleccionar(rol)}
            >
              <strong>{rol.nombre}</strong>
              <span>{rol.descripcion || "Sin descripción"}</span>
            </button>
            {puedeAdministrar && (
              <div>
                <button type="button" onClick={() => onEditar(rol)}>
                  Editar
                </button>
                {!rol.es_sistema && (
                  <button type="button" onClick={() => onEliminar(rol)}>
                    Eliminar
                  </button>
                )}
              </div>
            )}
          </li>
        ))}
      </ul>
      {roles.length === 0 && <p role="status">No se encontraron roles.</p>}
    </section>
  );
}
