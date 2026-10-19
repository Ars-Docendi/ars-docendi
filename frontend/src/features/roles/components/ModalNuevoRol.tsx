import { useState } from "react";
import { Button, Checkbox, Field, Input, InlineAlert, Modal } from "@ars-docendi/ui";
import {
  ETIQUETAS_SCOPE,
  normalizarNombreRol,
  SCOPES_ROL,
  type DatosRolNuevo,
  type RolMock,
} from "../models";

interface ModalNuevoRolProps {
  open: boolean;
  rolesExistentes: RolMock[];
  nombresExistentes: string[];
  onCrear: (datos: DatosRolNuevo, rolBaseId: string | null) => void;
  onCerrar: () => void;
  error?: string;
  guardando?: boolean;
}

const VACIO: DatosRolNuevo = {
  nombre: "",
  descripcion: "",
  scope: "global",
};

export function ModalNuevoRol({
  open,
  rolesExistentes,
  nombresExistentes,
  onCrear,
  onCerrar,
  error,
  guardando = false,
}: ModalNuevoRolProps) {
  const [campos, setCampos] = useState(VACIO);
  const [enviado, setEnviado] = useState(false);
  const [usarBase, setUsarBase] = useState(false);
  const [rolBaseId, setRolBaseId] = useState<string>("");

  function handleCerrar() {
    setCampos(VACIO);
    setEnviado(false);
    setUsarBase(false);
    setRolBaseId("");
    onCerrar();
  }

  function handleConfirmar() {
    setEnviado(true);
    if (guardando || !campos.nombre.trim()) return;
    if (nombresExistentes.map(normalizarNombreRol).includes(normalizarNombreRol(campos.nombre)))
      return;
    onCrear(
      {
        nombre: campos.nombre.trim(),
        descripcion: campos.descripcion.trim(),
        scope: campos.scope,
      },
      usarBase && rolBaseId ? rolBaseId : null,
    );
  }

  const nombreDuplicado =
    enviado &&
    !!campos.nombre.trim() &&
    nombresExistentes.map(normalizarNombreRol).includes(normalizarNombreRol(campos.nombre));

  return (
    <Modal
      open={open}
      onOpenChange={(next) => {
        if (!next) handleCerrar();
      }}
      title="Nuevo rol"
      footer={
        <div
          style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: "1rem" }}
        >
          <Button
            variant="secondary"
            style={{
              background: "var(--danger-500)",
              color: "#fff",
              borderColor: "var(--danger-500)",
            }}
            onClick={handleCerrar}
          >
            Cancelar
          </Button>
          <Button variant="primary" disabled={guardando} onClick={handleConfirmar}>
            Crear rol
          </Button>
        </div>
      }
    >
      {error && <InlineAlert severity="danger" title={error} />}
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <Field
          label="Nombre"
          required
          error={enviado && !campos.nombre.trim() ? "Campo obligatorio" : undefined}
        >
          <Input
            value={campos.nombre}
            onChange={(e) => setCampos((p) => ({ ...p, nombre: e.target.value }))}
            placeholder="Ej: Coordinador de Área"
          />
        </Field>
        {nombreDuplicado && (
          <InlineAlert severity="danger" title="Ya existe un rol con ese nombre." />
        )}

        <Field label="Descripción">
          <Input
            value={campos.descripcion}
            onChange={(e) => setCampos((p) => ({ ...p, descripcion: e.target.value }))}
            placeholder="Descripción del rol y sus responsabilidades"
          />
        </Field>

        <Field label="Ámbito" required>
          <select
            value={campos.scope}
            onChange={(e) =>
              setCampos((prev) => ({
                ...prev,
                scope: e.target.value as DatosRolNuevo["scope"],
              }))
            }
            className="adoc-select"
            style={{ width: "100%" }}
          >
            {SCOPES_ROL.map((scope) => (
              <option key={scope} value={scope}>
                {ETIQUETAS_SCOPE[scope]}
              </option>
            ))}
          </select>
        </Field>

        <InlineAlert severity="info" title="Alcance del rol personalizado">
          Este rol puede agrupar permisos, pero no habilita a aceptar, rechazar ni devolver pedidos
          en el circuito de aprobación de designaciones.
        </InlineAlert>

        <div style={{ borderTop: "1px solid var(--color-border)", paddingTop: "1rem" }}>
          <Checkbox
            label="Usar un rol existente como base (hereda sus permisos)"
            checked={usarBase}
            onChange={(e) => {
              setUsarBase(e.target.checked);
              if (!e.target.checked) setRolBaseId("");
            }}
          />
          {usarBase && (
            <div style={{ marginTop: "0.75rem" }}>
              <Field label="Rol base">
                <select
                  value={rolBaseId}
                  onChange={(e) => setRolBaseId(e.target.value)}
                  className="adoc-select"
                  style={{ width: "100%" }}
                >
                  <option value="">Seleccioná un rol...</option>
                  {rolesExistentes.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.nombre}
                    </option>
                  ))}
                </select>
              </Field>
            </div>
          )}
        </div>
      </div>
    </Modal>
  );
}
