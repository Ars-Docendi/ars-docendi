import { useState } from "react";
import { Button, Field, InlineAlert, Modal, Select } from "@ars-docendi/ui";
import { MembresiasSelector, type MembresiaFila } from "../../../shared/ui/MembresiasSelector";
import {
  nombreCompleto,
  type AsignacionMateria,
  type CargoDocente,
  type DocenteMock,
  type PersonaSistema,
  type MateriaMock,
  type RolCatalogoDocente,
} from "../models";
import { AsignacionesSelector, type AsignacionRow } from "./AsignacionesSelector";
import { CamposPersonaDocente, type CamposPersonaDocenteDatos } from "./CamposPersonaDocente";

const CAMPOS_PERSONA_VACIOS: CamposPersonaDocenteDatos = {
  nombre: "",
  apellido: "",
  documento: "",
  legajo: "",
  cuil: "",
  fecha_nacimiento: "",
  telefono: "",
  upn: "",
};

type Modo = "nueva" | "existente";

interface ModalNuevoDocenteProps {
  open: boolean;
  upnsExistentes: string[];
  onCrear: (datos: Omit<DocenteMock, "id" | "is_active">) => void;
  onCerrar: () => void;
  materias: MateriaMock[];
  cargos: string[];
  dedicaciones: { id: string; nombre: string }[];
  personas: PersonaSistema[];
  error?: string;
  rolesDisponibles: RolCatalogoDocente[];
}

function validarAsignaciones(rows: AsignacionRow[]): string | undefined {
  const completas = rows.filter((r) => r.materia && r.cargo && r.horas && Number(r.horas) > 0);
  if (completas.length === 0) return "Agregá al menos una asignación";
  if (
    rows.some(
      (r) =>
        !r.materia ||
        !r.cargo ||
        !r.horas ||
        Number(r.horas) <= 0 ||
        (!r.dedicacionId && !r.dedicacionLegada),
    )
  ) {
    return "Completá o quitá las filas incompletas (materia, cargo, dedicación y horas > 0)";
  }
  return undefined;
}

function validarMembresias(filas: MembresiaFila[]): string | undefined {
  if (filas.length === 0) return "Seleccioná al menos una membresía";
  if (
    new Set(filas.map((fila) => `${fila.rolId}:${fila.materiaId}:${fila.carreraId}`)).size !==
    filas.length
  ) {
    return "No se puede repetir la misma membresía";
  }
  if (filas.some((fila) => !fila.rolId || !fila.materiaId || !fila.carreraId)) {
    return "Completá las filas de membresía";
  }
  return undefined;
}

export function ModalNuevoDocente({
  open,
  upnsExistentes,
  onCrear,
  onCerrar,
  materias,
  cargos,
  dedicaciones,
  personas,
  error,
  rolesDisponibles,
}: ModalNuevoDocenteProps) {
  const [modo, setModo] = useState<Modo>("nueva");
  const [personaId, setPersonaId] = useState("");
  const [campos, setCampos] = useState(CAMPOS_PERSONA_VACIOS);
  const [membresias, setMembresias] = useState<MembresiaFila[]>([
    { rolId: "", materiaId: "", carreraId: "" },
  ]);
  const [asignacionRows, setAsignacionRows] = useState<AsignacionRow[]>([
    { materia: "", cargo: "", horas: "", dedicacionId: "" },
  ]);
  const [enviado, setEnviado] = useState(false);

  function handleCerrar() {
    setModo("nueva");
    setPersonaId("");
    setCampos(CAMPOS_PERSONA_VACIOS);
    setMembresias([{ rolId: "", materiaId: "", carreraId: "" }]);
    setAsignacionRows([{ materia: "", cargo: "", horas: "", dedicacionId: "" }]);
    setEnviado(false);
    onCerrar();
  }

  function handleModo(nuevoModo: Modo) {
    setModo(nuevoModo);
    setPersonaId("");
    setCampos(CAMPOS_PERSONA_VACIOS);
    setEnviado(false);
  }

  function seleccionarPersona(id: string) {
    setPersonaId(id);
    const persona = personas.find((p) => p.id === id) ?? null;
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
        : CAMPOS_PERSONA_VACIOS,
    );
  }

  function set<K extends keyof CamposPersonaDocenteDatos>(campo: K, valor: string) {
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
    const errorMembresias = validarMembresias(membresias);
    if (!personaOk || errorMembresias || errorAsignaciones) return;
    if (upnsExistentes.includes(campos.upn.toLowerCase())) return;

    const asignaciones: AsignacionMateria[] = asignacionRows
      .filter((r) => r.materia && r.cargo && r.horas && Number(r.horas) > 0)
      .map((r) => ({
        materia: materias.find((m) => m.codigo === r.materia)!,
        cargo: r.cargo as CargoDocente,
        horas: Number(r.horas),
        dedicacionId: r.dedicacionId || null,
      }));

    onCrear({
      ...campos,
      upn: campos.upn.toLowerCase(),
      roles: [],
      membresias: membresias.map((fila) => ({
        id: "",
        codigo: "",
        nombre: "",
        ambito: "materia",
        rolId: fila.rolId,
        materiaId: fila.materiaId,
        carreraId: fila.carreraId,
      })),
      asignaciones,
      tieneCuenta: true,
      persona_id: modo === "existente" ? personaId : undefined,
    });
    handleCerrar();
  }

  const personaSeleccionada: PersonaSistema | null =
    modo === "existente" ? (personas.find((p) => p.id === personaId) ?? null) : null;

  const upnDuplicada = enviado && !!campos.upn && upnsExistentes.includes(campos.upn.toLowerCase());
  const errorAsignaciones = enviado ? validarAsignaciones(asignacionRows) : undefined;
  const errorMembresias = enviado ? validarMembresias(membresias) : undefined;

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
      {error && <InlineAlert severity="danger" title={error} />}
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
          <CamposPersonaDocente
            campos={campos}
            enviado={enviado}
            upnDuplicada={upnDuplicada}
            onChange={set}
          />
        ) : (
          <>
            <Field
              label="Seleccioná la persona"
              required
              error={enviado && !personaId ? "Campo obligatorio" : undefined}
            >
              <Select value={personaId} onChange={(e) => seleccionarPersona(e.target.value)}>
                <option value="">Elegí una persona del sistema…</option>
                {personas.map((p) => (
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
                aria-label="Datos personales de solo lectura"
              >
                <div
                  style={{
                    marginBottom: "0.25rem",
                    color: "var(--color-text-secondary)",
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    letterSpacing: "0.04em",
                    textTransform: "uppercase",
                  }}
                >
                  Datos personales · solo lectura
                </div>
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

        <MembresiasSelector
          filas={membresias}
          onChange={setMembresias}
          roles={rolesDisponibles}
          materias={materias}
          error={errorMembresias}
        />

        <AsignacionesSelector
          rows={asignacionRows}
          onChange={setAsignacionRows}
          error={errorAsignaciones}
          materias={materias}
          cargos={cargos}
          dedicaciones={dedicaciones}
        />
      </div>
    </Modal>
  );
}
