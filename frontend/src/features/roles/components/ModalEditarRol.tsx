import { useState } from "react";
import { Button, Field, Input, InlineAlert, Modal } from "@ars-docendi/ui";
import { ETIQUETAS_SCOPE, SCOPES_ROL, type DatosRolEditables, type RolMock } from "../models";

interface ModalEditarRolProps {
  rol: RolMock | null;
  nombresExistentes: string[];
  onGuardar: (datos: DatosRolEditables) => void;
  onCerrar: () => void;
}

export function ModalEditarRol({
  rol,
  nombresExistentes,
  onGuardar,
  onCerrar,
}: ModalEditarRolProps) {
  const [campos, setCampos] = useState<DatosRolEditables>({
    nombre: "",
    descripcion: "",
    scope: "global",
  });
  const [enviado, setEnviado] = useState(false);
  const [prevRol, setPrevRol] = useState<RolMock | null>(null);

  if (rol !== prevRol) {
    setPrevRol(rol);
    setCampos({
      nombre: rol?.nombre ?? "",
      descripcion: rol?.descripcion ?? "",
      scope: rol?.scope ?? "global",
    });
    setEnviado(false);
  }

  function handleCerrar() {
    setEnviado(false);
    onCerrar();
  }

  function handleConfirmar() {
    setEnviado(true);
    if (!campos.nombre.trim() || !campos.descripcion.trim()) return;
    if (nombresExistentes.map((n) => n.toLowerCase()).includes(campos.nombre.trim().toLowerCase()))
      return;
    onGuardar({
      nombre: campos.nombre.trim(),
      descripcion: campos.descripcion.trim(),
      scope: campos.scope,
    });
  }

  const nombreDuplicado =
    enviado &&
    !!campos.nombre.trim() &&
    nombresExistentes.map((n) => n.toLowerCase()).includes(campos.nombre.trim().toLowerCase());

  return (
    <Modal
      open={!!rol}
      onOpenChange={(next) => {
        if (!next) handleCerrar();
      }}
      title="Editar rol"
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
          <Button variant="primary" onClick={handleConfirmar}>
            Guardar
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <Field
          label="Nombre"
          required
          error={enviado && !campos.nombre.trim() ? "Campo obligatorio" : undefined}
        >
          <Input
            value={campos.nombre}
            onChange={(e) => setCampos((p) => ({ ...p, nombre: e.target.value }))}
          />
        </Field>
        {nombreDuplicado && (
          <InlineAlert severity="danger" title="Ya existe un rol con ese nombre." />
        )}

        <Field
          label="Descripción"
          required
          error={enviado && !campos.descripcion.trim() ? "Campo obligatorio" : undefined}
        >
          <Input
            value={campos.descripcion}
            onChange={(e) => setCampos((p) => ({ ...p, descripcion: e.target.value }))}
          />
        </Field>

        <Field label="Ámbito" required>
          <select
            value={campos.scope}
            onChange={(e) =>
              setCampos((prev) => ({
                ...prev,
                scope: e.target.value as DatosRolEditables["scope"],
              }))
            }
            disabled={rol?.es_sistema}
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

        {rol?.es_sistema && (
          <InlineAlert severity="info" title="Rol de sistema">
            El código y el ámbito de este rol son inmutables. Su nombre y descripción sí pueden
            editarse.
          </InlineAlert>
        )}
      </div>
    </Modal>
  );
}
