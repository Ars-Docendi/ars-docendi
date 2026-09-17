import { useState } from "react";
import { Button, DatePicker, Field, Input, InlineAlert, Modal } from "@ars-docendi/ui";
import { MembresiasSelector, type MembresiaFila } from "../../../shared/ui/MembresiasSelector";
import type { CatalogosUsuarios } from "../api/usuariosApi";
import type { UsuarioFormulario } from "../models";

interface ModalNuevoUsuarioProps {
  open: boolean;
  upnsExistentes: string[];
  onCrear: (datos: UsuarioFormulario) => void;
  onCerrar: () => void;
  error?: string;
  catalogos: CatalogosUsuarios;
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
  membresias: [] as MembresiaFila[],
};

function validarMembresias(
  filas: MembresiaFila[],
  catalogos: CatalogosUsuarios,
): string | undefined {
  if (filas.length === 0) return "Seleccioná al menos una membresía";
  if (
    new Set(filas.map((fila) => `${fila.rolId}:${fila.materiaId}:${fila.carreraId}`)).size !==
    filas.length
  ) {
    return "No se puede repetir la misma membresía";
  }
  if (
    filas.some((fila) => {
      const rol = catalogos.roles.find((opcion) => opcion.id === fila.rolId);
      return (
        !rol ||
        (rol.ambito === "materia" && (!fila.materiaId || !fila.carreraId)) ||
        (rol.ambito === "carrera" && !fila.carreraId) ||
        (rol.ambito === "global" && (fila.materiaId || fila.carreraId))
      );
    })
  )
    return "Completá las filas de membresía con un ámbito compatible";
  return undefined;
}

export function ModalNuevoUsuario({
  open,
  upnsExistentes,
  onCrear,
  onCerrar,
  error,
  catalogos,
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

  function handleConfirmar() {
    setEnviado(true);
    const obligatorios =
      !campos.nombre ||
      !campos.apellido ||
      !campos.documento ||
      !campos.legajo ||
      !campos.fecha_nacimiento ||
      !campos.upn;
    const errorMembresias = validarMembresias(campos.membresias, catalogos);
    if (obligatorios || errorMembresias) return;
    if (upnsExistentes.includes(campos.upn.toLowerCase())) return;
    onCrear({
      ...campos,
      upn: campos.upn.toLowerCase(),
      membresias: campos.membresias.map((fila) => ({
        rolId: fila.rolId,
        materiaId: fila.materiaId || null,
        carreraId: fila.carreraId || null,
      })),
    });
    setCampos(VACIO);
    setEnviado(false);
  }

  const upnDuplicada = enviado && !!campos.upn && upnsExistentes.includes(campos.upn.toLowerCase());
  const errorMembresias = enviado ? validarMembresias(campos.membresias, catalogos) : undefined;

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
        {error && <InlineAlert severity="danger" title={error} />}
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

        <MembresiasSelector
          filas={campos.membresias}
          onChange={(membresias) => set("membresias", membresias)}
          roles={catalogos.roles}
          materias={catalogos.materias}
          carreras={catalogos.carreras}
          error={errorMembresias}
        />
      </div>
    </Modal>
  );
}
