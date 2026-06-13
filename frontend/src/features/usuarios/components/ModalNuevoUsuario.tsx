import { useState } from "react";
import { Button, Checkbox, DatePicker, Field, Input, InlineAlert, Modal } from "@ars-docendi/ui";
import { ROLES_SISTEMA, type RolSistema, type UsuarioMock } from "../mock/mockStore";

interface ModalNuevoUsuarioProps {
  open: boolean;
  upnsExistentes: string[];
  onCrear: (datos: Omit<UsuarioMock, "id" | "is_active">) => void;
  onCerrar: () => void;
}

const VACIO = {
  nombre: "",
  apellido: "",
  documento: "",
  legajo: "",
  cuil: "",
  fecha_nacimiento: "",
  telefono: "",
  upn: "",
  roles: [] as RolSistema[],
};

export function ModalNuevoUsuario({
  open,
  upnsExistentes,
  onCrear,
  onCerrar,
}: ModalNuevoUsuarioProps) {
  const [campos, setCampos] = useState(VACIO);
  const [enviado, setEnviado] = useState(false);

  function set<K extends keyof typeof VACIO>(campo: K, valor: (typeof VACIO)[K]) {
    setCampos((p) => ({ ...p, [campo]: valor }));
  }

  function handleCerrar() {
    setCampos(VACIO);
    setEnviado(false);
    onCerrar();
  }

  function toggleRol(rol: RolSistema, checked: boolean) {
    setCampos((p) => ({
      ...p,
      roles: checked ? [...p.roles, rol] : p.roles.filter((r) => r !== rol),
    }));
  }

  function handleConfirmar() {
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
    onCrear({ ...campos, upn: campos.upn.toLowerCase() });
    setCampos(VACIO);
    setEnviado(false);
  }

  const upnDuplicada = enviado && !!campos.upn && upnsExistentes.includes(campos.upn.toLowerCase());

  const grilla: React.CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: "1.25rem",
  };

  return (
    <Modal
      open={open}
      onOpenChange={(next) => {
        if (!next) handleCerrar();
      }}
      title="Nuevo usuario"
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
            onClick={handleCerrar}
          >
            Cancelar
          </Button>
          <Button variant="primary" onClick={handleConfirmar}>
            Crear usuario
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {/* Datos personales */}
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

        {/* Datos de contacto */}
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
                <InlineAlert severity="danger" title="Ya existe un usuario con esa UPN." />
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

        {/* Roles */}
        <Field
          label="Roles"
          required
          error={enviado && campos.roles.length === 0 ? "Seleccioná al menos un rol" : undefined}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "8px", marginTop: "4px" }}>
            {ROLES_SISTEMA.map((r) => (
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
