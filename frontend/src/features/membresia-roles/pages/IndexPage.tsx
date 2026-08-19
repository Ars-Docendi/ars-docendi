import { useState, useMemo } from "react";
import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { useRoles, useMembresiaRol } from "../../roles/hooks/useRoles";
import { ListaRoles } from "../components/ListaRoles";
import { PanelPermisos } from "../components/PanelPermisos";
import { normalizarTexto, type RolMock } from "../../roles/models";
import type { MapaMembresias } from "../models";
const SIN_ROLES: RolMock[] = [];

export function IndexPage() {
  const remotoRoles = useRoles();
  const roles = remotoRoles.consulta.data ?? SIN_ROLES;
  const [busqueda, setBusqueda] = useState("");
  const [rolSeleccionado, setRolSeleccionado] = useState<RolMock | null>(null);
  const membresia = useMembresiaRol(rolSeleccionado);
  const [borradores, setBorradores] = useState<Record<string, string[]>>({});
  const seleccionados = rolSeleccionado
    ? (borradores[rolSeleccionado.id] ?? membresia.asignados.data?.map((p) => p.id) ?? [])
    : [];
  const membresias: MapaMembresias = rolSeleccionado ? { [rolSeleccionado.id]: seleccionados } : {};
  const permisos = (membresia.catalogo.data ?? []).map((p) => ({
    id: p.id,
    nombre: p.nombre,
    desc: p.descripcion,
  }));

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
      {remotoRoles.consulta.isLoading && <p role="status">Cargando roles…</p>}
      {(remotoRoles.consulta.isError ||
        membresia.catalogo.isError ||
        membresia.guardar.isError) && (
        <p role="alert">No se pudo cargar o guardar la membresía. Reintentá la operación.</p>
      )}
      {membresia.guardar.isPending && <p role="status">Guardando permisos…</p>}

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
            permisos={permisos}
            membresias={membresias}
            onToggle={(rolId, permisoId) =>
              setBorradores((actuales) => {
                const seleccion = actuales[rolId] ?? seleccionados;
                return {
                  ...actuales,
                  [rolId]: seleccion.includes(permisoId)
                    ? seleccion.filter((id) => id !== permisoId)
                    : [...seleccion, permisoId],
                };
              })
            }
            onGuardar={async () => {
              await membresia.guardar.mutateAsync(seleccionados);
            }}
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
