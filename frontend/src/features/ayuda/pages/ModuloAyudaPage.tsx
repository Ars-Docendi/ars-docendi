import { Navigate, useParams } from "react-router-dom";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { FilaEnlaceAyuda } from "../components/FilaEnlaceAyuda";
import { MigasAyuda } from "../components/MigasAyuda";
import { useAyudaVisible } from "../hooks/useAyudaVisible";
import { RUTA_AYUDA, rutaArticulo } from "../utils/rutas";

export function ModuloAyudaPage() {
  const { modulo: moduloSlug } = useParams();
  const { modulos, articulos } = useAyudaVisible();
  const modulo = modulos.find((candidato) => candidato.slug === moduloSlug);
  if (!modulo) return <Navigate to={RUTA_AYUDA} replace />;

  const delModulo = articulos.filter((articulo) => articulo.moduloSlug === modulo.slug);
  const grupos = modulo.grupos
    .map((grupo) => ({ grupo, articulos: delModulo.filter((a) => a.grupo === grupo) }))
    .filter(({ articulos: delGrupo }) => delGrupo.length > 0);

  return (
    <div className="ayuda-pagina">
      <MigasAyuda items={[{ etiqueta: modulo.titulo }]} />
      <PageHeader title={modulo.titulo} meta={modulo.descripcion} />
      {grupos.map(({ grupo, articulos: delGrupo }) => (
        <section key={grupo} className="ayuda-seccion">
          <h2 className="ayuda-modulo-grupo">{grupo}</h2>
          <ul className="ayuda-lista">
            {delGrupo.map((articulo) => (
              <FilaEnlaceAyuda
                key={articulo.slug}
                a={rutaArticulo(modulo.slug, articulo.slug)}
                titulo={articulo.titulo}
                descripcion={articulo.resumen}
              />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
