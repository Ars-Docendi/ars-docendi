import { Field, Input, InlineAlert } from "@ars-docendi/ui";
import type { CSSProperties } from "react";

export interface CamposPersonaDocenteDatos {
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fecha_nacimiento: string;
  telefono: string;
  upn: string;
}

interface CamposPersonaDocenteProps {
  campos: CamposPersonaDocenteDatos;
  enviado: boolean;
  upnDuplicada: boolean;
  onChange: <K extends keyof CamposPersonaDocenteDatos>(campo: K, valor: string) => void;
  mensajeUpnDuplicada?: string;
}

export function CamposPersonaDocente({
  campos,
  enviado,
  upnDuplicada,
  onChange,
  mensajeUpnDuplicada = "Ya existe un docente con esa UPN.",
}: CamposPersonaDocenteProps) {
  const grilla: CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: "1.25rem",
  };
  const campo = (nombre: keyof CamposPersonaDocenteDatos) =>
    enviado && !campos[nombre] ? "Campo obligatorio" : undefined;

  return (
    <>
      <div style={grilla}>
        <Field label="Nombre" required error={campo("nombre")}>
          <Input
            value={campos.nombre}
            onChange={(e) => onChange("nombre", e.target.value)}
            placeholder="Ej: María"
          />
        </Field>
        <Field label="Apellido" required error={campo("apellido")}>
          <Input
            value={campos.apellido}
            onChange={(e) => onChange("apellido", e.target.value)}
            placeholder="Ej: González"
          />
        </Field>
      </div>
      <div style={grilla}>
        <Field label="Documento (DNI)" required error={campo("documento")}>
          <Input
            value={campos.documento}
            onChange={(e) => onChange("documento", e.target.value)}
            placeholder="Ej: 30123456"
          />
        </Field>
        <Field label="Legajo" required error={campo("legajo")}>
          <Input
            value={campos.legajo}
            onChange={(e) => onChange("legajo", e.target.value)}
            placeholder="Ej: 0421"
          />
        </Field>
      </div>
      <div style={grilla}>
        <Field label="CUIL">
          <Input
            value={campos.cuil}
            onChange={(e) => onChange("cuil", e.target.value)}
            placeholder="Ej: 27-30123456-4"
          />
        </Field>
        <Field label="Fecha de nacimiento" required error={campo("fecha_nacimiento")}>
          <Input
            type="date"
            value={campos.fecha_nacimiento}
            onChange={(e) => onChange("fecha_nacimiento", e.target.value)}
          />
        </Field>
      </div>
      <div style={grilla}>
        <div style={{ gridColumn: "span 2" }}>
          <Field label="UPN / Email institucional" required error={campo("upn")}>
            <Input
              type="email"
              value={campos.upn}
              onChange={(e) => onChange("upn", e.target.value)}
              placeholder="nombre@unlam.edu.ar"
            />
          </Field>
          {upnDuplicada && (
            <div style={{ marginTop: "6px" }}>
              <InlineAlert severity="danger" title={mensajeUpnDuplicada} />
            </div>
          )}
        </div>
      </div>
      <Field label="Teléfono">
        <Input
          value={campos.telefono}
          onChange={(e) => onChange("telefono", e.target.value)}
          placeholder="Ej: 11-4523-8801"
        />
      </Field>
    </>
  );
}
