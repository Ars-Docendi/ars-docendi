import { useState, useMemo } from "react";
import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { useConfiguracion } from "../../../shared/configuracion/useConfiguracion";
import { ListaRoles } from "../components/ListaRoles";
import { PanelPermisos } from "../components/PanelPermisos";
import { normalizarTexto, type RolMock } from "../../roles/mock/mockStore";
import { PERMISOS_INICIALES } from "../mock/mockStore";

export function IndexPage() {
  const { roles, membresias, togglePermiso } = useConfiguracion();
  const [busqueda, setBusqueda] = useState("");
  const [rolSeleccionado, setRolSeleccionado] = useState<RolMock | null>(null);

  const rolesFiltrados = useMemo(() => {
    const q = normalizarTexto(busqueda);
    if (!q) return roles;
    return roles.filter((r) => normalizarTexto(r.nombre).includes(q));
  }, [roles, busqueda]);

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: "Membresía Roles" }]}
      />
      <PageHeader title="Membresía Roles" meta="Asigná permisos a cada rol" />

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "260px 1fr",
          gap: "24px",
          alignItems: "start",
        }}
      >
        <ListaRoles
          roles={rolesFiltrados}
          busqueda={busqueda}
          onBusquedaChange={setBusqueda}
          rolSeleccionadoId={rolSeleccionado?.id ?? null}
          onSeleccionar={setRolSeleccionado}
        />

        {rolSeleccionado ? (
          <PanelPermisos
            rol={rolSeleccionado}
            permisos={PERMISOS_INICIALES}
            membresias={membresias}
            onToggle={togglePermiso}
            onGuardar={() => {}}
          />
        ) : (
          <div
            style={{
              background: "var(--color-bg-raised)",
              border: "1px solid var(--color-border-default)",
              borderRadius: "var(--radius-sm)",
              padding: "48px 32px",
              textAlign: "center",
              color: "var(--color-text-tertiary)",
            }}
          >
            <div style={{ fontSize: "28px", marginBottom: "12px", opacity: 0.35 }}>☰</div>
            <p style={{ fontSize: "14px", margin: 0 }}>
              Seleccioná un rol para ver y editar sus permisos
            </p>
          </div>
        )}
      </div>
    </>
  );
}
