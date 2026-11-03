import { PageHeader } from "../../../shared/ui/PageHeader";
import { FilaEnlaceAyuda } from "../components/FilaEnlaceAyuda";
import { MigasAyuda } from "../components/MigasAyuda";
import { useAyudaVisible } from "../hooks/useAyudaVisible";
import { rutaModulo } from "../utils/rutas";

export function InicioAyudaPage() {
  const { modulos } = useAyudaVisible();
  return (
    <div className="ayuda-pagina">
      <MigasAyuda items={[]} />
      <PageHeader
        title="Centro de ayuda"
        meta="Guías paso a paso de las pantallas del sistema que podés usar."
      />
      {modulos.length > 0 ? (
        <ul className="ayuda-lista">
          {modulos.map((modulo) => (
            <FilaEnlaceAyuda
              key={modulo.slug}
              a={rutaModulo(modulo.slug)}
              titulo={modulo.titulo}
              descripcion={modulo.descripcion}
            />
          ))}
        </ul>
      ) : (
        <p className="ayuda-vacio">Todavía no hay artículos de ayuda disponibles para tu perfil.</p>
      )}
    </div>
  );
}
