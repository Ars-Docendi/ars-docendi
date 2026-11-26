import { useState } from "react";
import { Button, Select } from "@ars-docendi/ui";

import type { OrdenURL } from "../navegacion/estadoURL";
import { HojaFiltros, type GrupoFiltro, type ValoresFiltro } from "./HojaFiltros";
import "./HerramientasLista.css";
import { IconoFilter, IconoSearch, IconoX } from "./iconos";

export interface OpcionOrden<C extends string> {
  columna: C;
  direccion: "asc" | "desc";
  etiqueta: string;
}

interface HerramientasListaProps<T extends ValoresFiltro<T>, C extends string> {
  grupos: GrupoFiltro<T>[];
  filtros: T;
  vacios: T;
  onFiltrosChange: (filtros: T) => void;
  contar?: (filtros: T) => number;
  /** Filter key edited by the search field (it does not appear in the sheet). */
  busqueda?: { clave: keyof T & string; placeholder: string };
  ordenes?: OpcionOrden<C>[];
  orden?: OrdenURL<C> | null;
  onOrdenChange?: (orden: OrdenURL<C>) => void;
}

const valorOrden = (o: { columna: string; direccion: string }) => `${o.columna}:${o.direccion}`;

/**
 * Phone toolbar of a list: search, "Filtros (n)" button with its bottom sheet, native sort select
 * and the applied filters as removable chips. Everything reads and writes the page's existing
 * filter and sort state.
 */
export function HerramientasLista<T extends ValoresFiltro<T>, C extends string>({
  grupos,
  filtros,
  vacios,
  onFiltrosChange,
  contar,
  busqueda,
  ordenes,
  orden,
  onOrdenChange,
}: HerramientasListaProps<T, C>) {
  const [hojaAbierta, setHojaAbierta] = useState(false);

  const chips = grupos.flatMap((grupo) => {
    const valor = filtros[grupo.clave];
    if (grupo.tipo === "texto") {
      return typeof valor === "string" && valor.trim()
        ? [
            {
              clave: `${grupo.clave}`,
              etiqueta: `${grupo.titulo}: ${valor}`,
              quitar: () => onFiltrosChange({ ...filtros, [grupo.clave]: "" }),
            },
          ]
        : [];
    }
    return (valor as string[]).map((v) => ({
      clave: `${grupo.clave}:${v}`,
      etiqueta: grupo.opciones.find((o) => o.valor === v)?.etiqueta ?? v,
      quitar: () =>
        onFiltrosChange({ ...filtros, [grupo.clave]: (valor as string[]).filter((x) => x !== v) }),
    }));
  });
  const hayAlgo =
    chips.length > 0 || (busqueda ? Boolean(String(filtros[busqueda.clave]).trim()) : false);
  const valorSelect = orden ? valorOrden(orden) : ordenes ? valorOrden(ordenes[0]) : "";

  return (
    <div className="adoc-herramientas-lista">
      {busqueda && (
        <div className="adoc-herramientas-busqueda">
          <IconoSearch />
          <input
            type="search"
            inputMode="search"
            enterKeyHint="search"
            autoComplete="off"
            className="adoc-input"
            aria-label="Buscar"
            placeholder={busqueda.placeholder}
            value={filtros[busqueda.clave] as string}
            onChange={(e) => onFiltrosChange({ ...filtros, [busqueda.clave]: e.target.value })}
          />
        </div>
      )}
      <div className="adoc-herramientas-fila">
        {grupos.length > 0 && (
          <Button variant="secondary" aria-haspopup="dialog" onClick={() => setHojaAbierta(true)}>
            <IconoFilter />
            Filtros
            {chips.length > 0 && (
              <span className="adoc-herramientas-contador" aria-label={`${chips.length} activos`}>
                {chips.length}
              </span>
            )}
          </Button>
        )}
        {ordenes && ordenes.length > 0 && onOrdenChange && (
          <Select
            aria-label="Ordenar por"
            value={valorSelect}
            onChange={(e) => {
              const elegida = ordenes.find((o) => valorOrden(o) === e.target.value);
              if (elegida)
                onOrdenChange({ columna: elegida.columna, direccion: elegida.direccion });
            }}
          >
            {ordenes.map((o) => (
              <option key={valorOrden(o)} value={valorOrden(o)}>
                {o.etiqueta}
              </option>
            ))}
          </Select>
        )}
      </div>
      {hayAlgo && (
        <div className="adoc-herramientas-chips" role="group" aria-label="Filtros aplicados">
          {chips.map((chip) => (
            <button
              key={chip.clave}
              type="button"
              className="adoc-chip-filtro"
              aria-label={`Quitar filtro ${chip.etiqueta}`}
              onClick={chip.quitar}
            >
              {chip.etiqueta}
              <IconoX />
            </button>
          ))}
          <button
            type="button"
            className="adoc-chips-limpiar"
            onClick={() => onFiltrosChange(vacios)}
          >
            Limpiar filtros
          </button>
        </div>
      )}
      <HojaFiltros
        abierta={hojaAbierta}
        onCerrar={() => setHojaAbierta(false)}
        grupos={grupos}
        filtros={filtros}
        vacios={vacios}
        contar={contar}
        onAplicar={onFiltrosChange}
      />
    </div>
  );
}
