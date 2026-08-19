import type { RolMock } from "../../roles/models";

interface ListaRolesProps {
  roles: RolMock[];
  busqueda: string;
  onBusquedaChange: (valor: string) => void;
  rolSeleccionadoId: string | null;
  onSeleccionar: (rol: RolMock) => void;
}

export function ListaRoles({
  roles,
  busqueda,
  onBusquedaChange,
  rolSeleccionadoId,
  onSeleccionar,
}: ListaRolesProps) {
  return (
    <div
      style={{
        background: "var(--color-bg-raised)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-sm)",
        overflow: "hidden",
      }}
    >
      <div style={{ padding: "10px 14px", borderBottom: "1px solid var(--color-border-default)" }}>
        <input
          type="search"
          placeholder="Buscar rol…"
          value={busqueda}
          onChange={(e) => onBusquedaChange(e.target.value)}
          style={{
            width: "100%",
            padding: "6px 10px",
            border: "1px solid var(--color-border-strong)",
            borderRadius: "var(--radius-xs)",
            fontSize: "13px",
            fontFamily: "var(--font-sans)",
            background: "var(--color-bg-raised)",
            color: "var(--color-text-primary)",
            outline: "none",
          }}
        />
      </div>
      <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
        {roles.map((rol) => {
          const seleccionado = rolSeleccionadoId === rol.id;
          return (
            <li
              key={rol.id}
              onClick={() => onSeleccionar(rol)}
              style={{
                padding: "10px 16px",
                cursor: "pointer",
                fontSize: "14px",
                borderBottom: "1px solid var(--color-border-default)",
                display: "flex",
                alignItems: "center",
                color: seleccionado ? "var(--accent-700)" : "var(--color-text-secondary)",
                background: seleccionado ? "var(--accent-100)" : "transparent",
                fontWeight: seleccionado ? 600 : 400,
                borderLeft: seleccionado
                  ? "3px solid var(--color-accent)"
                  : "3px solid transparent",
              }}
            >
              {rol.nombre}
            </li>
          );
        })}
        {roles.length === 0 && (
          <li
            style={{
              padding: "16px",
              fontSize: "13px",
              color: "var(--color-text-tertiary)",
              textAlign: "center",
            }}
          >
            No se encontraron roles.
          </li>
        )}
      </ul>
    </div>
  );
}
