import { useId, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Input } from "@ars-docendi/ui";

import { useAyudaVisible } from "../hooks/useAyudaVisible";
import { buscar } from "../utils/busqueda";
import { rutaArticulo, rutaBusqueda } from "../utils/rutas";

const MAXIMO_EN_PANEL = 5;
const SIN_ACTIVA = -1;

/**
 * Combobox de búsqueda (patrón ARIA 1.2) sobre los artículos visibles.
 * `consultaInicial` (el `?q=` de la página de resultados) se vuelve a volcar al campo
 * cada vez que cambia.
 */
export function BuscadorAyuda({ consultaInicial = "" }: { consultaInicial?: string }) {
  const navigate = useNavigate();
  const { articulos, modulos } = useAyudaVisible();
  const idLista = useId();
  const contenedor = useRef<HTMLDivElement>(null);
  const entrada = useRef<HTMLInputElement>(null);
  const [consulta, setConsulta] = useState(consultaInicial);
  const [abierto, setAbierto] = useState(false);
  const [activa, setActiva] = useState(SIN_ACTIVA);

  // Ajuste de estado en render (sin efecto): sincroniza el campo con la URL.
  const [inicialPrevia, setInicialPrevia] = useState(consultaInicial);
  if (consultaInicial !== inicialPrevia) {
    setInicialPrevia(consultaInicial);
    setConsulta(consultaInicial);
  }

  const buscada = consulta.trim();
  const coincidencias = useMemo(() => buscar(buscada, articulos), [buscada, articulos]);
  const enPanel = coincidencias.slice(0, MAXIMO_EN_PANEL);
  const mostrarPanel = abierto && buscada.length > 0;
  const idOpcion = (indice: number) => `${idLista}-opcion-${indice}`;
  const tituloModulo = (slug: string) => modulos.find((m) => m.slug === slug)?.titulo ?? "";

  function cerrar() {
    setAbierto(false);
    setActiva(SIN_ACTIVA);
  }

  function alTeclear(evento: KeyboardEvent<HTMLInputElement>) {
    if (evento.key === "Escape") return cerrar();
    if (evento.key === "Enter" && buscada) {
      evento.preventDefault();
      const elegida = enPanel[activa];
      navigate(elegida ? rutaArticulo(elegida.moduloSlug, elegida.slug) : rutaBusqueda(buscada));
      return cerrar();
    }
    if (enPanel.length === 0) return;
    if (evento.key === "ArrowDown") {
      evento.preventDefault();
      setAbierto(true);
      setActiva((actual) => (actual + 1) % enPanel.length);
    } else if (evento.key === "ArrowUp") {
      evento.preventDefault();
      setAbierto(true);
      setActiva((actual) => (actual <= 0 ? enPanel.length - 1 : actual - 1));
    }
  }

  return (
    <div
      ref={contenedor}
      className="ayuda-buscador"
      onBlur={(evento) => {
        if (!contenedor.current?.contains(evento.relatedTarget)) cerrar();
      }}
    >
      <div className="ayuda-buscador-campo">
        <Input
          ref={entrada}
          type="text"
          role="combobox"
          aria-label="Buscar en el centro de ayuda"
          aria-autocomplete="list"
          aria-expanded={mostrarPanel}
          aria-controls={mostrarPanel && enPanel.length > 0 ? idLista : undefined}
          aria-activedescendant={mostrarPanel && activa >= 0 ? idOpcion(activa) : undefined}
          placeholder="Buscar…"
          autoComplete="off"
          value={consulta}
          onChange={(evento) => {
            setConsulta(evento.target.value);
            setAbierto(true);
            setActiva(SIN_ACTIVA);
          }}
          onFocus={() => setAbierto(true)}
          onKeyDown={alTeclear}
        />
        {consulta && (
          <button
            type="button"
            className="ayuda-buscador-limpiar"
            aria-label="Borrar búsqueda"
            onClick={() => {
              setConsulta("");
              cerrar();
              entrada.current?.focus();
            }}
          >
            ×
          </button>
        )}
      </div>
      {mostrarPanel && (
        // El foco queda en el campo: Safari no enfoca el enlace al hacer clic y el blur cerraría
        // el panel antes de que llegue el click.
        <div className="ayuda-buscador-panel" onMouseDown={(evento) => evento.preventDefault()}>
          {enPanel.length > 0 ? (
            <div id={idLista} role="listbox" aria-label="Resultados de la búsqueda">
              {enPanel.map((articulo, indice) => (
                <Link
                  key={`${articulo.moduloSlug}/${articulo.slug}`}
                  id={idOpcion(indice)}
                  role="option"
                  aria-selected={indice === activa}
                  className="ayuda-buscador-opcion"
                  tabIndex={-1}
                  to={rutaArticulo(articulo.moduloSlug, articulo.slug)}
                  onClick={cerrar}
                >
                  <span className="titulo">{articulo.titulo}</span>
                  <span className="modulo">{tituloModulo(articulo.moduloSlug)}</span>
                </Link>
              ))}
            </div>
          ) : (
            <p className="ayuda-buscador-vacio" role="status">
              Sin resultados para «{buscada}»
            </p>
          )}
          {coincidencias.length > MAXIMO_EN_PANEL && (
            <Link className="ayuda-buscador-todos" to={rutaBusqueda(buscada)} onClick={cerrar}>
              Ver todos los resultados
            </Link>
          )}
        </div>
      )}
    </div>
  );
}
