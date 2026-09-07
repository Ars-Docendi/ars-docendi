import { useState } from "react";
import { Button, Field, InlineAlert, Modal, Tabs, type TabItem } from "@ars-docendi/ui";
import {
  nombreCompleto,
  type AsignacionMateria,
  type CargoDocente,
  type DocenteMock,
  type RolDocente,
  type MateriaMock,
} from "../models";
import { AsignacionesSelector, type AsignacionRow } from "./AsignacionesSelector";
import { CamposPersonaDocente, type CamposPersonaDocenteDatos } from "./CamposPersonaDocente";

interface ModalEditarDocenteProps {
  docente: DocenteMock | null;
  upnsExistentes: string[];
  onGuardar: (datos: Omit<DocenteMock, "id" | "is_active">) => void;
  onCerrar: () => void;
  materias: MateriaMock[];
  cargos: string[];
  error?: string;
  rolesDisponibles: string[];
}

const PESTAÑAS: TabItem[] = [
  { id: "docentes", label: "Datos docentes" },
  { id: "personales", label: "Datos personales" },
];

type PestañaId = "docentes" | "personales";

function camposDesde(d: DocenteMock | null): CamposPersonaDocenteDatos {
  return {
    nombre: d?.nombre ?? "",
    apellido: d?.apellido ?? "",
    documento: d?.documento ?? "",
    legajo: d?.legajo ?? "",
    cuil: d?.cuil ?? "",
    fecha_nacimiento: d?.fecha_nacimiento ?? "",
    telefono: d?.telefono ?? "",
    upn: d?.upn ?? "",
  };
}

function asignacionesDesde(d: DocenteMock | null): AsignacionRow[] {
  if (!d || d.asignaciones.length === 0) return [{ materia: "", cargo: "", horas: "" }];
  return d.asignaciones.map((a) => ({
    materia: a.materia.codigo,
    cargo: a.cargo,
    horas: String(a.horas),
  }));
}

function validarAsignaciones(rows: AsignacionRow[]): string | undefined {
  const completas = rows.filter((r) => r.materia && r.cargo && r.horas && Number(r.horas) > 0);
  if (completas.length === 0) return "Agregá al menos una asignación";
  if (rows.some((r) => !r.materia || !r.cargo || !r.horas || Number(r.horas) <= 0)) {
    return "Completá o quitá las filas incompletas (materia, cargo y horas > 0)";
  }
  return undefined;
}

export function ModalEditarDocente({
  docente,
  upnsExistentes,
  onGuardar,
  onCerrar,
  materias,
  cargos,
  error,
  rolesDisponibles,
}: ModalEditarDocenteProps) {
  const [prevDocente, setPrevDocente] = useState(docente);
  const [campos, setCampos] = useState(camposDesde(docente));
  const [roles, setRoles] = useState<string[]>(docente?.roles ?? []);
  const [asignacionRows, setAsignacionRows] = useState<AsignacionRow[]>(asignacionesDesde(docente));
  const [enviado, setEnviado] = useState(false);
  const [pestaña, setPestaña] = useState<PestañaId>("docentes");

  if (docente !== prevDocente) {
    setPrevDocente(docente);
    setCampos(camposDesde(docente));
    setRoles(docente?.roles ?? []);
    setAsignacionRows(asignacionesDesde(docente));
    setEnviado(false);
    setPestaña("docentes");
  }

  function set<K extends keyof CamposPersonaDocenteDatos>(campo: K, valor: string) {
    setCampos((p) => ({ ...p, [campo]: valor }));
  }

  function handleGuardar() {
    setEnviado(true);
    const obligatorios =
      !campos.nombre ||
      !campos.apellido ||
      !campos.documento ||
      !campos.legajo ||
      !campos.fecha_nacimiento ||
      !campos.upn;
    const errorAsignaciones = validarAsignaciones(asignacionRows);
    if (obligatorios || roles.length === 0 || errorAsignaciones) return;
    if (upnsExistentes.includes(campos.upn.toLowerCase())) return;

    const asignaciones: AsignacionMateria[] = asignacionRows
      .filter((r) => r.materia && r.cargo && r.horas && Number(r.horas) > 0)
      .map((r) => ({
        materia: materias.find((m) => m.codigo === r.materia)!,
        cargo: r.cargo as CargoDocente,
        horas: Number(r.horas),
      }));

    onGuardar({
      ...campos,
      upn: campos.upn.toLowerCase(),
      roles: roles as RolDocente[],
      asignaciones,
    });
  }

  const upnDuplicada = enviado && !!campos.upn && upnsExistentes.includes(campos.upn.toLowerCase());
  const errorAsignaciones = enviado ? validarAsignaciones(asignacionRows) : undefined;
  const hayErroresPersonales =
    enviado &&
    (!campos.nombre ||
      !campos.apellido ||
      !campos.documento ||
      !campos.legajo ||
      !campos.fecha_nacimiento ||
      !campos.upn ||
      upnDuplicada);

  return (
    <Modal
      open={docente !== null}
      onOpenChange={(next) => {
        if (!next) onCerrar();
      }}
      title="Editar docente"
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
          {docente ? nombreCompleto(docente) : ""} &mdash; {docente?.upn}
        </p>
      </div>

      <Tabs
        items={PESTAÑAS}
        value={pestaña}
        onChange={(id) => setPestaña(id as PestañaId)}
        style={{ marginBottom: "1.25rem" }}
      />

      {/* Pestaña: Datos docentes */}
      {pestaña === "docentes" && (
        <div role="tabpanel" id="panel-docentes" aria-labelledby="tab-docentes">
          <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            {hayErroresPersonales && (
              <InlineAlert
                severity="warning"
                title='Hay campos incompletos en la pestaña "Datos personales".'
              />
            )}

            <Field
              label="Roles"
              required
              error={enviado && roles.length === 0 ? "Seleccioná al menos un rol" : undefined}
            >
              <div style={{ display: "flex", gap: "1.5rem", padding: "0.25rem 0" }}>
                {rolesDisponibles.map((r) => (
                  <label
                    key={r}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.375rem",
                      cursor: "pointer",
                      fontSize: "0.875rem",
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={roles.includes(r)}
                      onChange={(e) =>
                        setRoles(e.target.checked ? [...roles, r] : roles.filter((x) => x !== r))
                      }
                    />
                    {r}
                  </label>
                ))}
              </div>
            </Field>

            <AsignacionesSelector
              rows={asignacionRows}
              onChange={setAsignacionRows}
              error={errorAsignaciones}
              materias={materias}
              cargos={cargos}
            />
          </div>
        </div>
      )}

      {/* Pestaña: Datos personales */}
      {pestaña === "personales" && (
        <div role="tabpanel" id="panel-personales" aria-labelledby="tab-personales">
          <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            <CamposPersonaDocente
              campos={campos}
              enviado={enviado}
              upnDuplicada={upnDuplicada}
              onChange={set}
              mensajeUpnDuplicada="Ya existe otro docente con esa UPN."
            />
          </div>
        </div>
      )}
    </Modal>
  );
}
