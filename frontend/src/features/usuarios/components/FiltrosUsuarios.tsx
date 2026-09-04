import { useState } from "react";
import { Input, Select } from "@ars-docendi/ui";
import { type RolSistema } from "../models";

export interface FiltrosState {
  apellido: string;
  nombre: string;
  documento: string;
  legajo: string;
  mail: string;
  rol: RolSistema | "";
  estado: "activo" | "inactivo" | "";
}

type FiltroOpcional = "legajo" | "mail" | "rol" | "estado";

const ETIQUETAS: Record<FiltroOpcional, string> = {
  legajo: "Legajo",
  mail: "Mail / UPN",
  rol: "Rol",
  estado: "Estado",
};

const TODOS_OPCIONALES: FiltroOpcional[] = ["legajo", "mail", "rol", "estado"];

const estiloBotonQuitar: React.CSSProperties = {
  border: "1px solid var(--color-border-default)",
  background: "#fff",
  borderRadius: "var(--radius-xs)",
  width: "28px",
  height: "36px",
  cursor: "pointer",
  color: "var(--color-text-secondary)",
  fontSize: "14px",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  flexShrink: 0,
};

interface FiltrosUsuariosProps {
  filtros: FiltrosState;
  onChange: (filtros: FiltrosState) => void;
  roles: string[];
}

export function FiltrosUsuarios({ filtros, onChange, roles }: FiltrosUsuariosProps) {
  const [activados, setActivados] = useState<FiltroOpcional[]>([]);

  function set<K extends keyof FiltrosState>(campo: K, valor: FiltrosState[K]) {
    onChange({ ...filtros, [campo]: valor });
  }

  function agregarFiltro(filtro: FiltroOpcional) {
    setActivados((prev) => [...prev, filtro]);
  }

  function quitarFiltro(filtro: FiltroOpcional) {
    setActivados((prev) => prev.filter((f) => f !== filtro));
    set(filtro, "");
  }

  const disponibles = TODOS_OPCIONALES.filter((f) => !activados.includes(f));

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.625rem",
        margin: "1.25rem 0 1.5rem",
        padding: "1rem 1.25rem",
        background: "var(--color-surface-raised, #f5f5f5)",
        borderRadius: "var(--radius-md, 6px)",
      }}
    >
      {/* Fila 1 — filtros fijos: Apellido, Nombre, Documento + selector */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "0.75rem", alignItems: "center" }}>
        <Input
          style={{ flex: "1 1 150px", minWidth: "0" }}
          placeholder="Filtrar por apellido…"
          value={filtros.apellido}
          onChange={(e) => set("apellido", e.target.value)}
          aria-label="Filtrar por apellido"
        />
        <Input
          style={{ flex: "1 1 150px", minWidth: "0" }}
          placeholder="Filtrar por nombre…"
          value={filtros.nombre}
          onChange={(e) => set("nombre", e.target.value)}
          aria-label="Filtrar por nombre"
        />
        <Input
          style={{ flex: "1 1 140px", minWidth: "0" }}
          placeholder="Filtrar por documento…"
          value={filtros.documento}
          onChange={(e) => set("documento", e.target.value)}
          aria-label="Filtrar por documento"
        />
        {disponibles.length > 0 && (
          <div className="adoc-select-wrap" style={{ flex: "0 0 auto" }}>
            <select
              className="adoc-select"
              value=""
              onChange={(e) => {
                if (e.target.value) agregarFiltro(e.target.value as FiltroOpcional);
              }}
              aria-label="Añadir filtro"
              style={{ width: "auto" }}
            >
              <option value="">+ Añadir filtro…</option>
              {disponibles.map((f) => (
                <option key={f} value={f}>
                  {ETIQUETAS[f]}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Fila 2 — filtros opcionales activos */}
      {activados.length > 0 && (
        <div style={{ display: "flex", flexWrap: "wrap", gap: "0.75rem", alignItems: "center" }}>
          {activados.includes("legajo") && (
            <div style={{ display: "flex", alignItems: "center", gap: "4px", flex: "0 0 auto" }}>
              <Input
                placeholder="Legajo…"
                value={filtros.legajo}
                onChange={(e) => set("legajo", e.target.value)}
                aria-label="Filtrar por legajo"
                style={{ width: "120px" }}
              />
              <button
                onClick={() => quitarFiltro("legajo")}
                aria-label="Quitar filtro de legajo"
                style={estiloBotonQuitar}
              >
                ×
              </button>
            </div>
          )}

          {activados.includes("mail") && (
            <div style={{ display: "flex", alignItems: "center", gap: "4px", flex: "0 0 auto" }}>
              <Input
                type="email"
                placeholder="Mail / UPN…"
                value={filtros.mail}
                onChange={(e) => set("mail", e.target.value)}
                aria-label="Filtrar por mail o UPN"
                style={{ width: "210px" }}
              />
              <button
                onClick={() => quitarFiltro("mail")}
                aria-label="Quitar filtro de mail"
                style={estiloBotonQuitar}
              >
                ×
              </button>
            </div>
          )}

          {activados.includes("rol") && (
            <div style={{ display: "flex", alignItems: "center", gap: "4px", flex: "0 0 auto" }}>
              <Select
                value={filtros.rol}
                onChange={(e) => set("rol", e.target.value as RolSistema | "")}
                aria-label="Filtrar por rol"
                style={{ width: "auto" }}
              >
                <option value="">Todos los roles</option>
                {roles.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </Select>
              <button
                onClick={() => quitarFiltro("rol")}
                aria-label="Quitar filtro de rol"
                style={estiloBotonQuitar}
              >
                ×
              </button>
            </div>
          )}

          {activados.includes("estado") && (
            <div style={{ display: "flex", alignItems: "center", gap: "4px", flex: "0 0 auto" }}>
              <Select
                value={filtros.estado}
                onChange={(e) => set("estado", e.target.value as "activo" | "inactivo" | "")}
                aria-label="Filtrar por estado"
                style={{ width: "auto" }}
              >
                <option value="">Todos los estados</option>
                <option value="activo">Activo</option>
                <option value="inactivo">Inactivo</option>
              </Select>
              <button
                onClick={() => quitarFiltro("estado")}
                aria-label="Quitar filtro de estado"
                style={estiloBotonQuitar}
              >
                ×
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
