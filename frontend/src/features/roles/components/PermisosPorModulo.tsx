import { useState } from "react";
import { Checkbox, Input } from "@ars-docendi/ui";

import { normalizarTexto, type PermisoRol } from "../models";
import { agruparPorModulo } from "../modulosPermisos";

interface PermisosPorModuloProps {
  catalogo: PermisoRol[];
  asignados: string[];
  deshabilitado: boolean;
  onChange: (asignados: string[]) => void;
}

/**
 * Permisos agrupados por módulo: un bloque desplegable por módulo, cerrado por defecto.
 * El buscador deja sólo los módulos con coincidencias y los despliega; el checkbox y el
 * contador del módulo siguen refiriéndose al módulo completo.
 */
export function PermisosPorModulo({
  catalogo,
  asignados,
  deshabilitado,
  onChange,
}: PermisosPorModuloProps) {
  const alternar = (id: string) =>
    onChange(asignados.includes(id) ? asignados.filter((x) => x !== id) : [...asignados, id]);
  const [busqueda, setBusqueda] = useState("");
  const q = normalizarTexto(busqueda.trim());
  const coincide = (texto: string) => normalizarTexto(texto).includes(q);
  const modulos = agruparPorModulo(catalogo)
    .map((modulo) => ({
      ...modulo,
      visibles:
        !q || coincide(modulo.nombre)
          ? modulo.permisos
          : modulo.permisos.filter((p) => coincide(p.nombre) || coincide(p.descripcion)),
    }))
    .filter((modulo) => modulo.visibles.length > 0);

  return (
    <div className="roles-modulos">
      <Input
        type="search"
        aria-label="Buscar permisos"
        placeholder="Buscar permiso o módulo…"
        value={busqueda}
        onChange={(e) => setBusqueda(e.target.value)}
      />
      {modulos.length === 0 && (
        <p className="roles-modulos-vacio">No hay permisos que coincidan con la búsqueda.</p>
      )}
      {modulos.map((modulo) => {
        const ids = modulo.permisos.map((p) => p.id);
        const marcados = ids.filter((id) => asignados.includes(id)).length;
        const completo = marcados === ids.length;
        return (
          // Con búsqueda el bloque se monta desplegado; al limpiarla vuelve a cerrarse.
          <details
            key={`${modulo.clave}-${q ? "busqueda" : ""}`}
            className="roles-modulo"
            open={Boolean(q)}
          >
            <summary>
              {/* El checkbox marca el módulo sin plegar ni desplegar el bloque. */}
              <span onClick={(e) => e.stopPropagation()}>
                <Checkbox
                  aria-label={`Todos los permisos de ${modulo.nombre}`}
                  checked={completo}
                  indeterminate={marcados > 0 && !completo}
                  disabled={deshabilitado}
                  onChange={() =>
                    onChange(
                      completo
                        ? asignados.filter((id) => !ids.includes(id))
                        : [...new Set([...asignados, ...ids])],
                    )
                  }
                />
              </span>
              <span className="roles-modulo-nombre">{modulo.nombre}</span>
              <span className="roles-modulo-contador">
                <strong>{marcados}</strong>/{ids.length}
              </span>
              <span className="roles-modulo-chevron" aria-hidden="true" />
            </summary>
            <ul>
              {modulo.visibles.map((permiso) => (
                <li key={permiso.id}>
                  <Checkbox
                    checked={asignados.includes(permiso.id)}
                    disabled={deshabilitado}
                    onChange={() => alternar(permiso.id)}
                    label={
                      <span className="roles-permiso">
                        <span>{permiso.nombre}</span>
                        <small>{permiso.descripcion}</small>
                      </span>
                    }
                  />
                </li>
              ))}
            </ul>
          </details>
        );
      })}
    </div>
  );
}
