import { useState } from "react";
import { Input, Select } from "@ars-docendi/ui";
import "./FiltrosLista.css";

interface OpcionSelectFiltro {
  value: string;
  label: string;
}

interface CampoFiltroFijoBase {
  clave: string;
  ariaLabel: string;
}

/** Campo siempre visible, en la primera fila del filtro: texto (default) o select. */
export type CampoFiltroFijo =
  | (CampoFiltroFijoBase & {
      tipo?: "texto";
      placeholder: string;
      /** "chica" para campos cortos (ids/números); por defecto ocupa el ancho disponible. */
      ancho?: "normal" | "chica";
    })
  | (CampoFiltroFijoBase & { tipo: "select"; opciones: OpcionSelectFiltro[] });

interface CampoFiltroOpcionalBase {
  clave: string;
  /** Texto del selector "+ Añadir filtro" y base de los `aria-label` derivados. */
  etiqueta: string;
  /** Valor al que vuelve el campo al quitarlo (por defecto ""; usar el valor "todos" de un select). */
  valorInicial?: string;
}

export type CampoFiltroOpcional =
  | (CampoFiltroOpcionalBase & { tipo: "texto"; placeholder: string; ancho?: string })
  | (CampoFiltroOpcionalBase & { tipo: "select"; opciones: OpcionSelectFiltro[] });

interface FiltrosListaProps<T extends Record<string, string>> {
  /** Campos de texto siempre visibles, en la primera fila. */
  fijos: CampoFiltroFijo[];
  /** Campos que se agregan opcionalmente vía "+ Añadir filtro" (texto o select). */
  opcionales: CampoFiltroOpcional[];
  valores: T;
  onChange: (valores: T) => void;
}

/**
 * Bloque de filtro genérico y reutilizable: campos de texto fijos + filtros
 * opcionales vía "+ Añadir filtro" (con botón "×" para quitarlos), sobre un
 * fondo gris — mismo patrón que ya usaban Usuarios y Mis pedidos por
 * separado, ahora un único componente config-driven para cualquier pantalla
 * con una lista filtrable.
 */
export function FiltrosLista<T extends Record<string, string>>({
  fijos,
  opcionales,
  valores,
  onChange,
}: FiltrosListaProps<T>) {
  const [activados, setActivados] = useState<string[]>([]);

  function set(clave: string, valor: string) {
    onChange({ ...valores, [clave]: valor });
  }

  function agregar(clave: string) {
    setActivados((prev) => [...prev, clave]);
  }

  function quitar(campo: CampoFiltroOpcional) {
    setActivados((prev) => prev.filter((c) => c !== campo.clave));
    set(campo.clave, campo.valorInicial ?? "");
  }

  const disponibles = opcionales.filter((o) => !activados.includes(o.clave));
  const activos = opcionales.filter((o) => activados.includes(o.clave));

  return (
    <div className="adoc-filtros">
      <div className="adoc-filtros-fila">
        {fijos.map((campo) =>
          campo.tipo === "select" ? (
            <span key={campo.clave} className="adoc-filtros-fijo-select">
              <Select
                value={valores[campo.clave] ?? ""}
                onChange={(e) => set(campo.clave, e.target.value)}
                aria-label={campo.ariaLabel}
              >
                {campo.opciones.map((op) => (
                  <option key={op.value} value={op.value}>
                    {op.label}
                  </option>
                ))}
              </Select>
            </span>
          ) : (
            <Input
              key={campo.clave}
              className={
                campo.ancho === "chica"
                  ? "adoc-filtros-input adoc-filtros-input--chica"
                  : "adoc-filtros-input"
              }
              placeholder={campo.placeholder}
              value={valores[campo.clave] ?? ""}
              onChange={(e) => set(campo.clave, e.target.value)}
              aria-label={campo.ariaLabel}
            />
          ),
        )}
        {disponibles.length > 0 && (
          <span className="adoc-filtros-add">
            <Select
              value=""
              onChange={(e) => {
                if (e.target.value) agregar(e.target.value);
              }}
              aria-label="Añadir filtro"
            >
              <option value="">+ Añadir filtro…</option>
              {disponibles.map((campo) => (
                <option key={campo.clave} value={campo.clave}>
                  {campo.etiqueta}
                </option>
              ))}
            </Select>
          </span>
        )}
      </div>

      {activos.length > 0 && (
        <div className="adoc-filtros-fila">
          {activos.map((campo) => (
            <span className="adoc-filtros-opcional" key={campo.clave}>
              {campo.tipo === "texto" ? (
                <Input
                  placeholder={campo.placeholder}
                  value={valores[campo.clave] ?? ""}
                  onChange={(e) => set(campo.clave, e.target.value)}
                  aria-label={`Filtrar por ${campo.etiqueta.toLowerCase()}`}
                  style={campo.ancho ? { width: campo.ancho } : undefined}
                />
              ) : (
                <Select
                  value={valores[campo.clave] ?? ""}
                  onChange={(e) => set(campo.clave, e.target.value)}
                  aria-label={`Filtrar por ${campo.etiqueta.toLowerCase()}`}
                >
                  {campo.opciones.map((op) => (
                    <option key={op.value} value={op.value}>
                      {op.label}
                    </option>
                  ))}
                </Select>
              )}
              <button
                type="button"
                className="adoc-filtros-quitar"
                onClick={() => quitar(campo)}
                aria-label={`Quitar filtro de ${campo.etiqueta.toLowerCase()}`}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
