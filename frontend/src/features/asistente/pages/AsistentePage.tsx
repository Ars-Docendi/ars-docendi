import { PageHeader } from "../../../shared/ui/PageHeader";
import { PanelAsistente } from "../components/PanelAsistente";
import "../asistente.css";

/**
 * El asistente a página completa. El otro montaje es el modal de la barra superior.
 *
 * La columna tiene alto fijo para que scrollee el hilo y no `.adoc-main` entero:
 * con la página scrolleando, el campo de entrada bajaba con cada respuesta y había
 * que perseguirlo, que es el defecto que el modal ya no tiene. El encabezado va
 * adentro de la columna para que tome su alto natural sin que haya que medirlo.
 */
export function AsistentePage() {
  return (
    <div className="adoc-asistente-pagina">
      <PageHeader
        title="Asistente"
        meta="Consultá en lenguaje natural lo que ya podés ver en el sistema"
      />
      <PanelAsistente />
    </div>
  );
}
