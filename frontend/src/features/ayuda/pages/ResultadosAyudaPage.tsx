import { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { FilaEnlaceAyuda } from "../components/FilaEnlaceAyuda";
import { MigasAyuda } from "../components/MigasAyuda";
import { useAyudaVisible } from "../hooks/useAyudaVisible";
import { buscar } from "../utils/busqueda";
import { RUTA_AYUDA, rutaArticulo } from "../utils/rutas";

export function ResultadosAyudaPage() {
  const [parametros] = useSearchParams();
  const consulta = (parametros.get("q") ?? "").trim();
  const { articulos, modulos } = useAyudaVisible();
  const resultados = useMemo(() => buscar(consulta, articulos), [consulta, articulos]);
  const tituloModulo = (slug: string) => modulos.find((m) => m.slug === slug)?.titulo ?? "";

  return (
    <div className="ayuda-pagina">
      <MigasAyuda items={[{ etiqueta: "Resultados" }]} />
      <PageHeader
        title="Resultados de la búsqueda"
        meta={
          resultados.length > 0
            ? `${resultados.length} ${resultados.length === 1 ? "resultado" : "resultados"} para «${consulta}»`
            : undefined
        }
      />
      {resultados.length > 0 ? (
        <ul className="ayuda-lista">
          {resultados.map((articulo) => (
            <FilaEnlaceAyuda
              key={`${articulo.moduloSlug}/${articulo.slug}`}
              a={rutaArticulo(articulo.moduloSlug, articulo.slug)}
              etiqueta={tituloModulo(articulo.moduloSlug)}
              titulo={articulo.titulo}
              descripcion={articulo.resumen}
            />
          ))}
        </ul>
      ) : (
        <div className="ayuda-vacio">
          <p>
            {consulta
              ? `No encontramos resultados para «${consulta}». Probá con otras palabras.`
              : "Escribí qué querés buscar en el buscador de la ayuda."}
          </p>
          <Link to={RUTA_AYUDA}>Volver al inicio de la ayuda</Link>
        </div>
      )}
    </div>
  );
}
