import { membresiaAGuardar, type MembresiaFila } from "../../shared/ui/membresias";
import type { CatalogosPersonas, GuardarPersona, Persona } from "./api/personasApi";
import { filaCompleta, type FilaAsignacion } from "./asignaciones";

export interface DatosPersonales {
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fechaNacimiento: string;
  upn: string;
  telefono: string;
}

export interface FormularioPersona {
  datos: DatosPersonales;
  membresias: MembresiaFila[];
  asignaciones: FilaAsignacion[];
}

export const CAMPOS: {
  campo: keyof DatosPersonales;
  etiqueta: string;
  requerido: boolean;
  placeholder?: string;
  tipo?: string;
  ancho?: boolean;
}[] = [
  { campo: "nombre", etiqueta: "Nombre", requerido: true, placeholder: "Ej: María" },
  { campo: "apellido", etiqueta: "Apellido", requerido: true, placeholder: "Ej: González" },
  { campo: "documento", etiqueta: "Documento (DNI)", requerido: true, placeholder: "Ej: 30123456" },
  { campo: "legajo", etiqueta: "Legajo", requerido: true, placeholder: "Ej: 0421" },
  { campo: "cuil", etiqueta: "CUIL", requerido: false, placeholder: "Ej: 27-30123456-4" },
  { campo: "fechaNacimiento", etiqueta: "Fecha de nacimiento", requerido: true, tipo: "date" },
  {
    campo: "upn",
    etiqueta: "UPN / Email institucional",
    requerido: true,
    placeholder: "nombre@unlam.edu.ar",
    tipo: "email",
    ancho: true,
  },
  { campo: "telefono", etiqueta: "Teléfono", requerido: false, placeholder: "Ej: 11-4523-8801" },
];

const MEMBRESIA_VACIA: MembresiaFila = { rolId: "", materiaId: "", carreraId: "" };

export function formularioVacio(): FormularioPersona {
  return {
    datos: {
      nombre: "",
      apellido: "",
      documento: "",
      legajo: "",
      cuil: "",
      fechaNacimiento: "",
      upn: "",
      telefono: "",
    },
    membresias: [{ ...MEMBRESIA_VACIA }],
    asignaciones: [],
  };
}

export function formularioDesde(p: Persona): FormularioPersona {
  return {
    datos: {
      nombre: p.nombre,
      apellido: p.apellido,
      documento: p.documento,
      legajo: p.legajo ?? "",
      cuil: p.cuil ?? "",
      fechaNacimiento: p.fechaNacimiento ?? "",
      upn: p.upn ?? "",
      telefono: p.telefono ?? "",
    },
    membresias: p.membresias.length
      ? p.membresias.map((m) => ({
          rolId: m.rolId,
          materiaId: m.materiaId ?? "",
          carreraId: m.carreraId ?? "",
        }))
      : [{ ...MEMBRESIA_VACIA }],
    asignaciones: p.asignaciones.map((a) => ({
      materiaId: a.materiaId,
      carreraId: a.carreraId,
      cargoId: a.cargoId,
      dedicacionId: a.dedicacionId ?? "",
      dedicacionLegada: a.dedicacionId ? null : a.dedicacion,
      horas: String(a.horas),
    })),
  };
}

/** Una membresía está completa cuando tiene el ámbito que pide su rol. */
export function membresiaCompleta(m: MembresiaFila, catalogos: CatalogosPersonas): boolean {
  const rol = catalogos.roles.find((r) => r.id === m.rolId);
  if (!rol) return false;
  if (rol.codigo === "docente") return Boolean(m.materiaId && m.carreraId);
  if (rol.ambito === "materia") return Boolean(m.materiaId);
  if (rol.ambito === "carrera") return Boolean(m.carreraId);
  return true;
}

export interface ErroresFormulario {
  campos: Partial<Record<keyof DatosPersonales, string>>;
  membresias?: string;
  asignaciones?: string;
}

export function validar(f: FormularioPersona, catalogos: CatalogosPersonas): ErroresFormulario {
  const campos: ErroresFormulario["campos"] = {};
  for (const c of CAMPOS)
    if (c.requerido && !f.datos[c.campo].trim()) campos[c.campo] = "Campo obligatorio";
  const membresias = f.membresias.filter((m) => membresiaCompleta(m, catalogos));
  const requiereAsignaciones = membresias.some((m) =>
    ["docente", "jefe_catedra"].includes(
      catalogos.roles.find((r) => r.id === m.rolId)?.codigo ?? "",
    ),
  );
  return {
    campos,
    membresias:
      membresias.length === 0 || membresias.length !== f.membresias.length
        ? "Toda persona necesita al menos un rol con su ámbito; completá o quitá los incompletos."
        : undefined,
    asignaciones:
      f.asignaciones.some((a) => !filaCompleta(a)) ||
      (requiereAsignaciones && f.asignaciones.length === 0)
        ? "Con rol Docente o Jefe de Cátedra hace falta al menos una asignación completa (materia, carrera, cargo, dedicación y horas > 0)."
        : undefined,
  };
}

export const hayErroresPersonales = (e: ErroresFormulario) => Object.keys(e.campos).length > 0;
export const hayErroresDocentes = (e: ErroresFormulario) => Boolean(e.membresias || e.asignaciones);

export function aGuardar(
  f: FormularioPersona,
  catalogos: CatalogosPersonas,
  version?: number | null,
): GuardarPersona {
  const opcional = (v: string) => (v.trim() ? v.trim() : null);
  return {
    nombre: f.datos.nombre.trim(),
    apellido: f.datos.apellido.trim(),
    documento: f.datos.documento.replace(/\D/g, ""),
    legajo: opcional(f.datos.legajo),
    cuil: opcional(f.datos.cuil),
    fechaNacimiento: opcional(f.datos.fechaNacimiento),
    telefono: opcional(f.datos.telefono),
    upn: f.datos.upn.trim(),
    membresias: f.membresias.map((m) => membresiaAGuardar(m, catalogos.roles)),
    designaciones: f.asignaciones.map((a) => ({
      materiaId: a.materiaId,
      carreraId: a.carreraId,
      cargoId: a.cargoId,
      dedicacionId: a.dedicacionId || null,
      horas: Number(a.horas),
    })),
    version,
  };
}
