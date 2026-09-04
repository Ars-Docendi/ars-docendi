import { useState } from "react";
import { Button, Input, Select, StatusBadge } from "@ars-docendi/ui";

import type { Tag } from "../types";
import { vocabularioDisponible } from "../mock/mockStore";
import { SeccionPerfil } from "./SeccionPerfil";
import "./portal.css";

interface SeccionTagsProps {
  titulo: string;
  tags: Tag[];
  onAgregar: (termino: string) => void;
  onQuitar: (termino: string) => void;
}

/**
 * Lista de tags del perfil. La usan Habilidades e Intereses por separado, con
 * el mismo vocabulario: ante una vacante son señales distintas y mezclarlas
 * perdería cuál es cuál.
 *
 * La librería no tiene Tag/Chip/Combobox, así que el widget se compone acá con
 * `Select` + lista, siguiendo el precedente de MateriasSelector.
 */
export function SeccionTags({ titulo, tags, onAgregar, onQuitar }: SeccionTagsProps) {
  const [editando, setEditando] = useState(false);
  const [sugerencia, setSugerencia] = useState("");

  const disponibles = vocabularioDisponible(tags);

  function confirmarSugerencia() {
    const limpio = sugerencia.trim();
    if (!limpio) return;
    onAgregar(limpio);
    setSugerencia("");
  }

  if (tags.length === 0 && !editando) {
    return (
      <SeccionPerfil
        titulo={titulo}
        vacia
        accion={{ etiqueta: "+ Agregar", onClick: () => setEditando(true) }}
      />
    );
  }

  return (
    <SeccionPerfil
      titulo={titulo}
      accion={
        editando
          ? { etiqueta: "Listo", onClick: () => setEditando(false) }
          : { etiqueta: "Editar", onClick: () => setEditando(true) }
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        <div className="portal-tags">
          {tags.map((tag) => (
            <span className="portal-tag" key={tag.termino}>
              {tag.termino}
              {tag.sugerido && <StatusBadge kind="pendiente" label="Sugerido" showIcon={false} />}
              {editando && (
                <button
                  type="button"
                  className="portal-tag-quitar"
                  aria-label={`Quitar ${tag.termino}`}
                  onClick={() => onQuitar(tag.termino)}
                >
                  ×
                </button>
              )}
            </span>
          ))}
        </div>

        {editando && (
          <div className="portal-form-grid">
            <Select
              value=""
              aria-label={`Agregar a ${titulo.toLowerCase()}`}
              onChange={(e) => {
                if (e.target.value) onAgregar(e.target.value);
              }}
            >
              <option value="">Elegí un término…</option>
              {disponibles.map((termino) => (
                <option value={termino} key={termino}>
                  {termino}
                </option>
              ))}
            </Select>
            <div style={{ display: "flex", gap: "var(--space-2)" }}>
              <Input
                value={sugerencia}
                placeholder="Sugerir uno nuevo"
                aria-label={`Sugerir un término nuevo para ${titulo.toLowerCase()}`}
                onChange={(e) => setSugerencia(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    confirmarSugerencia();
                  }
                }}
              />
              <Button variant="secondary" onClick={confirmarSugerencia}>
                Sugerir
              </Button>
            </div>
          </div>
        )}
      </div>
    </SeccionPerfil>
  );
}
