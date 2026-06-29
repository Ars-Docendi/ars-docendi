import { useState } from "react";
import { Button } from "@ars-docendi/ui";
import type { RolMock } from "../../roles/mock/mockStore";
import type { PermisoMock, MapaMembresias } from "../mock/mockStore";

interface PanelPermisosProps {
  rol: RolMock;
  permisos: PermisoMock[];
  membresias: MapaMembresias;
  onToggle: (rolId: string, permisoId: string) => void;
  onGuardar: () => void;
}

export function PanelPermisos({
  rol,
  permisos,
  membresias,
  onToggle,
  onGuardar,
}: PanelPermisosProps) {
  const [guardado, setGuardado] = useState(false);

  const activos = membresias[rol.id] ?? [];

  function handleGuardar() {
    onGuardar();
    setGuardado(true);
    setTimeout(() => setGuardado(false), 2500);
  }

  return (
    <div
      style={{
        background: "var(--color-bg-raised)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-sm)",
        overflow: "hidden",
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: "10px 16px",
          borderBottom: "1px solid var(--color-border-default)",
          background: "var(--color-bg-surface)",
          fontSize: "12px",
          fontWeight: 600,
          textTransform: "uppercase",
          letterSpacing: "0.04em",
          color: "var(--color-text-tertiary)",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <span>{rol.nombre}</span>
        <span
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            minWidth: "20px",
            height: "20px",
            padding: "0 8px",
            fontSize: "11px",
            fontWeight: 600,
            borderRadius: "50px",
            background: "var(--neutral-300)",
            color: "var(--color-text-secondary)",
          }}
        >
          {activos.length} de {permisos.length}
        </span>
      </div>

      {/* Lista de permisos */}
      {permisos.map((permiso) => (
        <div
          key={permiso.id}
          onClick={() => onToggle(rol.id, permiso.id)}
          style={{
            padding: "10px 20px",
            borderBottom: "1px solid var(--color-border-default)",
            display: "flex",
            alignItems: "center",
            gap: "16px",
            cursor: "pointer",
          }}
        >
          <input
            type="checkbox"
            readOnly
            checked={activos.includes(permiso.id)}
            style={{
              accentColor: "var(--color-accent)",
              width: "15px",
              height: "15px",
              flexShrink: 0,
              cursor: "pointer",
            }}
          />
          <div>
            <div style={{ fontSize: "14px", fontWeight: 500 }}>{permiso.nombre}</div>
            <div style={{ fontSize: "12px", color: "var(--color-text-tertiary)" }}>
              {permiso.desc}
            </div>
          </div>
        </div>
      ))}

      {/* Footer */}
      <div
        style={{
          padding: "10px 20px",
          background: "var(--color-bg-surface)",
          borderTop: "1px solid var(--color-border-default)",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <span
          style={{
            fontSize: "12px",
            color: guardado ? "var(--accent-600)" : "var(--color-text-tertiary)",
          }}
        >
          {guardado ? "✓ Cambios guardados" : "Los cambios se aplican al guardar"}
        </span>
        <Button variant="primary" size="sm" onClick={handleGuardar}>
          Guardar cambios
        </Button>
      </div>
    </div>
  );
}
