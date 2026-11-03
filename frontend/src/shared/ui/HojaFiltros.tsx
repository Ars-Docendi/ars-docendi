import { useState } from "react";
import { Button, Checkbox, Input } from "@ars-docendi/ui";

import { Drawer } from "./Drawer";
import "./HojaFiltros.css";

interface OpcionFiltro {
  valor: string;
  etiqueta: string;
}

interface GrupoBase<T> {
  clave: keyof T & string;
  titulo: string;
}

/** Multi-select group: the filter value is a `string[]`. */
export interface GrupoOpciones<T> extends GrupoBase<T> {
  tipo?: "opciones";
  opciones: OpcionFiltro[];
}

/** Free-text group: the filter value is a `string`. */
export interface GrupoTexto<T> extends GrupoBase<T> {
  tipo: "texto";
  placeholder?: string;
}

export type GrupoFiltro<T> = GrupoOpciones<T> | GrupoTexto<T>;

/** Filters made only of `string` and `string[]` values, like the ones `useFiltrosURL` stores. */
export type ValoresFiltro<T> = { [K in keyof T]: string | string[] };

interface HojaFiltrosProps<T extends ValoresFiltro<T>> {
  abierta: boolean;
  onCerrar: () => void;
  grupos: GrupoFiltro<T>[];
  filtros: T;
  vacios: T;
  /** Rows that pass the given filters, computed over the unpaginated list. Omit for server-side filters: the button then reads "Aplicar filtros". */
  contar?: (filtros: T) => number;
  onAplicar: (filtros: T) => void;
}

/**
 * Bottom sheet with every filter group of a page. Edits a local draft: nothing changes until
 * "Ver N resultados" applies it through the page's own filter state.
 */
export function HojaFiltros<T extends ValoresFiltro<T>>(props: HojaFiltrosProps<T>) {
  // The draft restarts from the applied filters each time the sheet opens.
  return props.abierta ? <Hoja {...props} /> : null;
}

function Hoja<T extends ValoresFiltro<T>>({
  onCerrar,
  grupos,
  filtros,
  vacios,
  contar,
  onAplicar,
}: HojaFiltrosProps<T>) {
  const [borrador, setBorrador] = useState<T>(filtros);
  const cambiar = (clave: keyof T & string, valor: string | string[]) =>
    setBorrador({ ...borrador, [clave]: valor });
  const conservados = (Object.keys(vacios) as (keyof T)[]).filter(
    (c) => !grupos.some((g) => g.clave === c),
  );
  const total = contar?.(borrador);

  return (
    <Drawer
      open
      onOpenChange={(abierto) => !abierto && onCerrar()}
      side="right"
      title="Filtros"
      className="adoc-hoja-filtros"
      footer={
        <>
          <Button
            variant="ghost"
            onClick={() => {
              // Search lives outside the sheet: clearing the sheet keeps what was typed there.
              setBorrador({
                ...vacios,
                ...Object.fromEntries(conservados.map((c) => [c, borrador[c]])),
              });
            }}
          >
            Limpiar
          </Button>
          <Button
            variant="primary"
            onClick={() => {
              onAplicar(borrador);
              onCerrar();
            }}
          >
            {total === undefined
              ? "Aplicar filtros"
              : total === 1
                ? "Ver 1 resultado"
                : `Ver ${total} resultados`}
          </Button>
        </>
      }
    >
      {grupos.map((grupo) => (
        <fieldset key={grupo.clave} className="adoc-hoja-grupo">
          <legend>{grupo.titulo}</legend>
          {grupo.tipo === "texto" ? (
            <Input
              placeholder={grupo.placeholder}
              aria-label={grupo.titulo}
              value={borrador[grupo.clave] as string}
              onChange={(e) => cambiar(grupo.clave, e.target.value)}
            />
          ) : (
            grupo.opciones.map((opcion) => {
              const marcados = borrador[grupo.clave] as string[];
              const marcada = marcados.includes(opcion.valor);
              return (
                <Checkbox
                  key={opcion.valor}
                  label={opcion.etiqueta}
                  checked={marcada}
                  onChange={() =>
                    cambiar(
                      grupo.clave,
                      marcada
                        ? marcados.filter((v) => v !== opcion.valor)
                        : [...marcados, opcion.valor],
                    )
                  }
                />
              );
            })
          )}
        </fieldset>
      ))}
    </Drawer>
  );
}
