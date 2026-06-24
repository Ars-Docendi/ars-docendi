import { useState } from "react";
import { Button, DatePicker, Field, Input, InlineAlert, Modal, Select } from "@ars-docendi/ui";
import {
  MATERIAS_CATALOGO,
  PERSONAS_SISTEMA,
  ROLES_DOCENTE,
  nombreCompleto,
  type AsignacionMateria,
  type CargoDocente,
  type DocenteMock,
  type PersonaSistema,
  type RolDocente,
} from "../mock/mockStore";
import { AsignacionesSelector, type AsignacionRow } from "./AsignacionesSelector";

type Modo = "nueva" | "existente";

interface ModalNuevoDocenteProps {
  open: boolean;
  upnsExistentes: string[];
  onCrear: (datos: Omit<DocenteMock, "id" | "is_active">) => void;
  onCerrar: () => void;
}

const VACIO_PERSONA = {
  nombre: "",
  apellido: "",
  documento: "",
  legajo: "",
  cuil: "",
  fecha_nacimiento: "",
  telefono: "",
  upn: "",
};

function validarAsignaciones(rows: AsignacionRow[]): string | undefined {
  const completas = rows.filter((r) => r.materia && r.cargo && r.horas && Number(r.horas) > 0);
  if (completas.length === 0) return "Agregá al menos una asignación";
  if (rows.some((r) => !r.materia || !r.cargo || !r.horas || Number(r.horas) <= 0)) {
    return "Completá o quitá las filas incompletas (materia, cargo y horas > 0)";
  }
  return undefined;
}

export function ModalNuevoDocente({
  open,
  upnsExistentes,
  onCrear,
  onCerrar,
}: ModalNuevoDocenteProps) {
  const [modo, setModo] = useState<Modo>("nueva");
  const [personaId, setPersonaId] = useState("");
  const [campos, setCampos] = useState(VACIO_PERSONA);
  const [rol, setRol] = useState<string>("");
  const [asignacionRows, setAsignacionRows] = useState<AsignacionRow[]>([
    { materia: "", cargo: "", horas: "" },
  ]);
  const [enviado, setEnviado] = useState(false);

  function handleCerrar() {
    setModo("nueva");
    setPersonaId("");
    setCampos(VACIO_PERSONA);
    setRol("");
    setAsignacionRows([{ materia: "", cargo: "", horas: "" }]);
    setEnviado(false);
    onCerrar();
  }

  function handleModo(nuevoModo: Modo) {
    setModo(nuevoModo);
    setPersonaId("");
    setCampos(VACIO_PERSONA);
    setEnviado(false);
  }

  function seleccionarPersona(id: string) {
    setPersonaId(id);
    const persona = PERSONAS_SISTEMA.find((p) => p.id === id) ?? null;
    setCampos(
      persona
        ? {
            nombre: persona.nombre,
            apellido: persona.apellido,
            documento: persona.documento,
            legajo: persona.legajo,
            cuil: persona.cuil,
            fecha_nacimiento: persona.fecha_nacimiento,
            telefono: persona.telefono,
            upn: persona.upn,
          }
        : VACIO_PERSONA,
    );
  }

  function set<K extends keyof typeof VACIO_PERSONA>(campo: K, valor: string) {
    setCampos((p) => ({ ...p, [campo]: valor }));
  }

  function handleConfirmar() {
    setEnviado(true);

    const personaOk =
      modo === "existente"
        ? !!personaId
        : !!(
            campos.nombre &&
            campos.apellido &&
            campos.documento &&
            campos.legajo &&
            campos.fecha_nacimiento &&
            campos.upn
          );

    const errorAsignaciones = validarAsignaciones(asignacionRows);
    if (!personaOk || !rol || errorAsignaciones) return;
    if (upnsExistentes.includes(campos.upn.toLowerCase())) return;

    const asignaciones: AsignacionMateria[] = asignacionRows
      .filter((r) => r.materia && r.cargo && r.horas && Number(r.horas) > 0)
      .map((r) => ({
        materia: MATERIAS_CATALOGO.find((m) => m.codigo === r.materia)!,
        cargo: r.cargo as CargoDocente,
        horas: Number(r.horas),
      }));

    onCrear({
      ...campos,
      upn: campos.upn.toLowerCase(),
      roles: [rol as RolDocente],
      asignaciones,
    });
    handleCerrar();
  }

  const personaSeleccionada: PersonaSistema | null =
    modo === "existente" ? (PERSONAS_SISTEMA.find((p) => p.id === personaId) ?? null) : null;

  const upnDuplicada = enviado && !!campos.upn && upnsExistentes.includes(campos.upn.toLowerCase());
  const errorAsignaciones = enviado ? validarAsignaciones(asignacionRows) : undefined;

  const grilla: React.CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: "1.25rem",
  };

  const estiloTab = (activo: boolean): React.CSSProperties => ({
    flex: 1,
    padding: "0.5rem",
    border: "1px solid var(--color-border-default)",
    borderRadius: "var(--radius-xs)",
    background: activo ? "var(--color-primary, #1a56db)" : "#fff",
    color: activo ? "#fff" : "var(--color-text-default)",
    cursor: "pointer",
    fontWeight: activo ? 600 : 400,
    fontSize: "0.875rem",
  });

  return (
    <Modal
      open={open}
      onOpenChange={(next) => {
        if (!next) handleCerrar();
      }}
      title="Nuevo docente"
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
            Crear docente
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {/* Selector de modo */}
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <button
            type="button"
            style={estiloTab(modo === "nueva")}
            onClick={() => handleModo("nueva")}
          >
            Nueva persona
          </button>
          <button
            type="button"
            style={estiloTab(modo === "existente")}
            onClick={() => handleModo("existente")}
          >
            Persona del sistema
          </button>
        </div>

        {/* Datos personales según modo */}
        {modo === "nueva" ? (
          <>
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
                    <InlineAlert severity="danger" title="Ya existe un docente con esa UPN." />
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
          </>
        ) : (
          <>
            <Field
              label="Seleccioná la persona"
              required
              error={enviado && !personaId ? "Campo obligatorio" : undefined}
            >
              <Select value={personaId} onChange={(e) => seleccionarPersona(e.target.value)}>
                <option value="">Elegí una persona del sistema…</option>
                {PERSONAS_SISTEMA.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.apellido}, {p.nombre} — DNI {p.documento}
                  </option>
                ))}
              </Select>
            </Field>
            {personaSeleccionada && (
              <div
                style={{
                  padding: "0.75rem 1rem",
                  background: "var(--color-surface-raised, #f5f5f5)",
                  borderRadius: "var(--radius-md, 6px)",
                  fontSize: "0.875rem",
                  lineHeight: 1.6,
                }}
              >
                <strong>{nombreCompleto(personaSeleccionada)}</strong>
                <br />
                DNI {personaSeleccionada.documento} · Legajo {personaSeleccionada.legajo}
                <br />
                {personaSeleccionada.upn}
              </div>
            )}
            {upnDuplicada && (
              <InlineAlert
                severity="danger"
                title="Esta persona ya está registrada como docente."
              />
            )}
          </>
        )}

        {/* Rol y asignaciones — comunes a ambos modos */}
        <Field label="Rol" required error={enviado && !rol ? "Campo obligatorio" : undefined}>
          <Select value={rol} onChange={(e) => setRol(e.target.value)}>
            <option value="">Seleccioná un rol…</option>
            {ROLES_DOCENTE.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </Select>
        </Field>

        <AsignacionesSelector
          rows={asignacionRows}
          onChange={setAsignacionRows}
          error={errorAsignaciones}
        />
      </div>
    </Modal>
  );
}
