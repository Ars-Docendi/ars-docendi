import { useState } from "react";
import { Link, useMatch, useSearchParams } from "react-router-dom";

import { useAyudaVisible } from "../hooks/useAyudaVisible";
import { RUTA_AYUDA, SLUG_RESERVADO, rutaArticulo } from "../utils/rutas";
import { BuscadorAyuda } from "./BuscadorAyuda";
import { ListaPasosAyuda } from "./ListaPasosAyuda";

/**
 * Sidebar de documentación: título, buscador, módulos colapsables con sus artículos
 * visibles y, bajo el artículo activo, la lista de sus pasos.
 */
export function BarraLateralAyuda({ pasoActivo }: { pasoActivo?: string }) {
  const { modulos, articulos } = useAyudaVisible();
  const enArticulo = useMatch("/ayuda/:modulo/:articulo");
  const enModulo = useMatch("/ayuda/:modulo");
  const enBusqueda = useMatch(`${RUTA_AYUDA}/${SLUG_RESERVADO}`);
  const [parametros] = useSearchParams();
  const consultaInicial = enBusqueda ? (parametros.get("q") ?? "") : "";
  const moduloActivo = (enArticulo ?? enModulo)?.params.modulo;
  const articuloActivo = enArticulo?.params.articulo;

  // El módulo del artículo abierto arranca expandido y se reabre al navegar hacia él;
  // el resto lo decide la persona. Ajuste de estado en render (sin efecto) al cambiar
  // de módulo activo.
  const [manual, setManual] = useState<Record<string, boolean>>({});
  const [moduloPrevio, setModuloPrevio] = useState(moduloActivo);
  if (moduloActivo !== moduloPrevio) {
    setModuloPrevio(moduloActivo);
    if (moduloActivo) setManual((previo) => ({ ...previo, [moduloActivo]: true }));
  }
  const estaAbierto = (slug: string) => manual[slug] ?? slug === moduloActivo;

  return (
    <nav className="ayuda-sidebar" aria-label="Documentación de ayuda">
      <Link to={RUTA_AYUDA} className="ayuda-sidebar-titulo">
        Centro de ayuda
      </Link>
      <BuscadorAyuda consultaInicial={consultaInicial} />
      <ul className="ayuda-sidebar-modulos">
        {modulos.map((modulo) => {
          const abierto = estaAbierto(modulo.slug);
          const idLista = `ayuda-modulo-${modulo.slug}`;
          return (
            <li key={modulo.slug}>
              <button
                type="button"
                className="ayuda-sidebar-modulo"
                aria-expanded={abierto}
                aria-controls={idLista}
                onClick={() => setManual((previo) => ({ ...previo, [modulo.slug]: !abierto }))}
              >
                {modulo.titulo}
              </button>
              {abierto && (
                <ul id={idLista} className="ayuda-sidebar-articulos">
                  {articulos
                    .filter((articulo) => articulo.moduloSlug === modulo.slug)
                    .map((articulo) => {
                      const activo =
                        modulo.slug === moduloActivo && articulo.slug === articuloActivo;
                      return (
                        <li key={articulo.slug}>
                          <Link
                            to={rutaArticulo(modulo.slug, articulo.slug)}
                            className={activo ? "activo" : undefined}
                            aria-current={activo ? "page" : undefined}
                          >
                            {articulo.titulo}
                          </Link>
                          {activo && (
                            <ListaPasosAyuda articulo={articulo} pasoActivo={pasoActivo} />
                          )}
                        </li>
                      );
                    })}
                </ul>
              )}
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
