import { useEffect, useMemo, useRef } from "react";
import { Navigate, useLocation, useOutletContext, useParams } from "react-router-dom";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { FilaEnlaceAyuda } from "../components/FilaEnlaceAyuda";
import { MigasAyuda } from "../components/MigasAyuda";
import { PasoArticulo } from "../components/PasoArticulo";
import { PreguntasFrecuentes } from "../components/PreguntasFrecuentes";
import type { ContextoLayoutAyuda } from "../components/contextoLayout";
import { useAyudaVisible } from "../hooks/useAyudaVisible";
import { usePasoActivo } from "../hooks/usePasoActivo";
import { RUTA_AYUDA, idPaso, rutaArticulo, rutaModulo } from "../utils/rutas";

function prefiereMenosMovimiento(): boolean {
  return window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
}

export function ArticuloAyudaPage() {
  const { modulo: moduloSlug, articulo: articuloSlug } = useParams();
  const { key, hash } = useLocation();
  const { fijarPasoActivo } = useOutletContext<ContextoLayoutAyuda>();
  const { modulos, articulos } = useAyudaVisible();
  const cabecera = useRef<HTMLElement>(null);
  const articuloMostrado = useRef<string>(undefined);

  const articulo = articulos.find((a) => a.moduloSlug === moduloSlug && a.slug === articuloSlug);
  const modulo = modulos.find((m) => m.slug === moduloSlug);
  const idsPasos = useMemo(() => articulo?.pasos.map((_, i) => idPaso(i + 1)) ?? [], [articulo]);

  usePasoActivo(idsPasos, fijarPasoActivo);

  // React Router no desplaza a anclas por sí solo: al abrir un artículo (o al cambiar
  // el hash) se lleva la vista al paso o a las preguntas, o al tope sin hash. Solo es
  // animado cuando ya se estaba leyendo ese mismo artículo.
  useEffect(() => {
    if (!articulo) return;
    const animado = Boolean(hash) && articuloMostrado.current === articulo.slug;
    articuloMostrado.current = articulo.slug;
    if (!hash) {
      // Sin ancla el artículo se abre arriba del todo, con las migas a la vista:
      // se reinicia el contenedor con scroll del shell, no se centra el encabezado.
      const contenedor = cabecera.current?.closest<HTMLElement>(".adoc-main");
      if (contenedor) contenedor.scrollTop = 0;
      else window.scrollTo?.(0, 0);
      return;
    }
    const destino = document.getElementById(hash.slice(1));
    destino?.scrollIntoView({
      behavior: animado && !prefiereMenosMovimiento() ? "smooth" : "auto",
      block: "start",
    });
  }, [articulo, hash, key]);

  if (!articulo || !modulo) return <Navigate to={RUTA_AYUDA} replace />;

  const relacionados = articulo.relacionados
    .map((ref) => articulos.find((a) => `${a.moduloSlug}/${a.slug}` === ref))
    .filter((a) => a !== undefined);

  return (
    <article className="ayuda-pagina">
      <MigasAyuda
        items={[
          { etiqueta: modulo.titulo, a: rutaModulo(modulo.slug) },
          { etiqueta: articulo.titulo },
        ]}
      />
      <header ref={cabecera} className="ayuda-articulo-cabecera">
        <PageHeader title={articulo.titulo} />
        <p className="ayuda-introduccion">{articulo.introduccion}</p>
      </header>

      {articulo.pasos.map((paso, indice) => (
        <PasoArticulo key={`${articulo.slug}-${indice}`} paso={paso} numero={indice + 1} />
      ))}

      {articulo.despues && (
        <section className="ayuda-seccion">
          <h2>{articulo.despues.titulo}</h2>
          <p>{articulo.despues.cuerpo}</p>
          {articulo.despues.viñetas && (
            <ul className="ayuda-vinetas">
              {articulo.despues.viñetas.map((item) => (
                <li key={item.termino}>
                  <strong>{item.termino}:</strong> {item.descripcion}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {articulo.preguntas && articulo.preguntas.length > 0 && (
        <PreguntasFrecuentes preguntas={articulo.preguntas} />
      )}

      {relacionados.length > 0 && (
        <section className="ayuda-seccion">
          <h2>Artículos relacionados</h2>
          <ul className="ayuda-lista">
            {relacionados.map((relacionado) => (
              <FilaEnlaceAyuda
                key={`${relacionado.moduloSlug}/${relacionado.slug}`}
                a={rutaArticulo(relacionado.moduloSlug, relacionado.slug)}
                titulo={relacionado.titulo}
                descripcion={relacionado.resumen}
              />
            ))}
          </ul>
        </section>
      )}
    </article>
  );
}
