import { useState } from "react";
import { Button, Checkbox, DatePicker, Field, Input, InlineAlert, Modal } from "@ars-docendi/ui";
import { nombreCompleto, type RolSistema, type UsuarioMock } from "../models";

interface ModalEditarUsuarioProps {
  usuario: UsuarioMock | null;
  upnsExistentes: string[];
  onGuardar: (datos: Omit<UsuarioMock, "id" | "is_active">) => void;
  onCerrar: () => void;
  error?: string;
  rolesDisponibles: string[];
}

export function ModalEditarUsuario({
  usuario,
  upnsExistentes,
  onGuardar,
  onCerrar,
  error,
  rolesDisponibles,
}: ModalEditarUsuarioProps) {
  const [prevUsuario, setPrevUsuario] = useState(usuario);
  const [campos, setCampos] = useState(camposDesde(usuario));
  const [enviado, setEnviado] = useState(false);

  if (usuario !== prevUsuario) {
    setPrevUsuario(usuario);
    setCampos(camposDesde(usuario));
    setEnviado(false);
  }

  function set<K extends keyof ReturnType<typeof camposDesde>>(
    campo: K,
    valor: ReturnType<typeof camposDesde>[K],
  ) {
    setCampos((p) => ({ ...p, [campo]: valor }));
  }

  function toggleRol(rol: RolSistema, checked: boolean) {
    setCampos((p) => ({
      ...p,
      roles: checked ? [...p.roles, rol] : p.roles.filter((r) => r !== rol),
    }));
  }

  function handleGuardar() {
    setEnviado(true);
    const obligatorios =
      !campos.nombre ||
      !campos.apellido ||
      !campos.documento ||
      !campos.legajo ||
      !campos.fecha_nacimiento ||
      !campos.upn ||
      campos.roles.length === 0;
    if (obligatorios) return;
    if (upnsExistentes.includes(campos.upn.toLowerCase())) return;
    onGuardar({ ...campos, upn: campos.upn.toLowerCase() });
  }

  const upnDuplicada = enviado && !!campos.upn && upnsExistentes.includes(campos.upn.toLowerCase());

  const grilla: React.CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: "1.25rem",
  };

  return (
    <Modal
      open={usuario !== null}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Editar usuario"
      footer={
        <div
          className="adoc-modal-actions"
          style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: "1rem" }}
        >
          <Button
            variant="secondary"
            style={{
              background: "var(--danger-500)",
              color: "#fff",
              borderColor: "var(--danger-500)",
            }}
            onClick={onCerrar}
          >
            Cancelar
          </Button>
          <Button variant="primary" onClick={handleGuardar}>
            Guardar cambios
          </Button>
        </div>
      }
    >
      {error && <InlineAlert severity="danger" title={error} />}
      <div style={{ marginBottom: "1rem" }}>
        <p style={{ margin: 0, color: "var(--color-text-secondary)", fontSize: "0.875rem" }}>
          {usuario ? nombreCompleto(usuario) : ""} &mdash; {usuario?.upn}
        </p>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={grilla}>
          <Field
            label="Nombre"
            required
            error={enviado && !campos.nombre ? "Campo obligatorio" : undefined}
          >
            <Input
              value={campos.nombre}
              onChange={(e) => set("nombre", e.target.value)}
              placeholder="Ej: María"
            />
          </Field>
          <Field
            label="Apellido"
            required
            error={enviado && !campos.apellido ? "Campo obligatorio" : undefined}
          >
            <Input
              value={campos.apellido}
              onChange={(e) => set("apellido", e.target.value)}
              placeholder="Ej: González"
            />
          </Field>
        </div>

        <div style={grilla}>
          <Field
            label="Documento (DNI)"
            required
            error={enviado && !campos.documento ? "Campo obligatorio" : undefined}
          >
            <Input
              value={campos.documento}
              onChange={(e) => set("documento", e.target.value)}
              placeholder="Ej: 30123456"
            />
          </Field>
          <Field
            label="Legajo"
            required
            error={enviado && !campos.legajo ? "Campo obligatorio" : undefined}
          >
            <Input
              value={campos.legajo}
              onChange={(e) => set("legajo", e.target.value)}
              placeholder="Ej: 0421"
            />
          </Field>
        </div>

        <div style={grilla}>
          <Field label="CUIL">
            <Input
              value={campos.cuil}
              onChange={(e) => set("cuil", e.target.value)}
              placeholder="Ej: 27-30123456-4"
            />
          </Field>
          <Field
            label="Fecha de nacimiento"
            required
            error={enviado && !campos.fecha_nacimiento ? "Campo obligatorio" : undefined}
          >
            <DatePicker
              value={campos.fecha_nacimiento}
              onChange={(e) => set("fecha_nacimiento", e.target.value)}
            />
          </Field>
        </div>

        <div style={grilla}>
          <div style={{ gridColumn: "span 2" }}>
            <Field
              label="UPN / Email institucional"
              required
              error={enviado && !campos.upn ? "Campo obligatorio" : undefined}
            >
              <Input
                type="email"
                value={campos.upn}
                onChange={(e) => set("upn", e.target.value)}
                placeholder="nombre@unlam.edu.ar"
              />
            </Field>
            {upnDuplicada && (
              <div style={{ marginTop: "6px" }}>
                <InlineAlert severity="danger" title="Ya existe otro usuario con esa UPN." />
              </div>
            )}
          </div>
        </div>

        <Field label="Teléfono">
          <Input
            value={campos.telefono}
            onChange={(e) => set("telefono", e.target.value)}
            placeholder="Ej: 11-4523-8801"
          />
        </Field>

        <Field
          label="Roles"
          required
          error={enviado && campos.roles.length === 0 ? "Seleccioná al menos un rol" : undefined}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "8px", marginTop: "4px" }}>
            {rolesDisponibles.map((r) => (
              <Checkbox
                key={r}
                label={r}
                checked={campos.roles.includes(r)}
                onChange={(e) => toggleRol(r, e.target.checked)}
              />
            ))}
          </div>
        </Field>
      </div>
    </Modal>
  );
}

function camposDesde(u: UsuarioMock | null) {
  return {
    nombre: u?.nombre ?? "",
    apellido: u?.apellido ?? "",
    documento: u?.documento ?? "",
    legajo: u?.legajo ?? "",
    cuil: u?.cuil ?? "",
    fecha_nacimiento: u?.fecha_nacimiento ?? "",
    telefono: u?.telefono ?? "",
    upn: u?.upn ?? "",
    roles: (u?.roles ?? []) as RolSistema[],
  };
}
