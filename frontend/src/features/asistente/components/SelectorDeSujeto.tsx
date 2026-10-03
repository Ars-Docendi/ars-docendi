import { useState } from "react";
import { Button, Input } from "@ars-docendi/ui";

import type { PersonaParaSoporte } from "../api/soporteHistorialApi";

interface SelectorDeSujetoProps {
  personas: PersonaParaSoporte[];
  cargando: boolean;
  seleccionado: string | null;
  onSeleccionar: (personaId: string) => void;
}

/**
 * A quién leerle el historial: busca entre las personas del sistema y elige
 * una.
 *
 * REUTILIZA EL ENDPOINT de administración de usuarios (`GET
 * /api/administracion/usuarios`, vía `buscarPersonasParaSoporte`) — tasks.md
 * 13.1 pide explícitamente no crear un buscador nuevo—, pero es un componente
 * PROPIO de esta feature: `features/asistente/` no importa nada de
 * `features/usuarios/` (react-features-guide, aislamiento de features).
 */
export function SelectorDeSujeto({
  personas,
  cargando,
  seleccionado,
  onSeleccionar,
}: SelectorDeSujetoProps) {
  const [busqueda, setBusqueda] = useState("");
  const filtradas = filtrarPersonas(personas, busqueda);

  return (
    <div className="adoc-asistente-soporte-selector">
      <Input
        type="search"
        value={busqueda}
        onChange={(e) => setBusqueda(e.target.value)}
        placeholder="Buscar por nombre, apellido o documento…"
        aria-label="Buscar a quién"
      />

      {cargando && <p>Buscando personas…</p>}

      <ul className="adoc-asistente-soporte-personas">
        {filtradas.map((persona) => (
          <li key={persona.id}>
            <Button
              type="button"
              variant={persona.id === seleccionado ? "primary" : "ghost"}
              size="sm"
              aria-pressed={persona.id === seleccionado}
              onClick={() => onSeleccionar(persona.id)}
            >
              {persona.apellido}, {persona.nombre} ({persona.documento})
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function normalizar(texto: string): string {
  return texto.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}

function filtrarPersonas(personas: PersonaParaSoporte[], busqueda: string): PersonaParaSoporte[] {
  const buscado = normalizar(busqueda.trim());
  if (!buscado) return personas;
  return personas.filter((persona) =>
    [persona.nombre, persona.apellido, persona.documento].some((valor) =>
      normalizar(valor).includes(buscado),
    ),
  );
}
