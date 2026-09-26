import { PageHeader } from "../../../shared/ui/PageHeader";
import { PanelAsistente } from "../components/PanelAsistente";
import { useAsistente } from "../hooks/useAsistente";
import { useHistorialAsistente } from "../hooks/useHistorialAsistente";
import "../asistente.css";

/**
 * El asistente a página completa. El otro montaje es el modal de la barra superior.
 *
 * SE VA CON ARS-151 (tasks.md §10): esta ruta se borra y `LanzadorAsistente` pasa a
 * ser el único montaje. Hasta entonces sigue viva y renderizando el mismo
 * `PanelAsistente` de v3 —rail, encabezado, grilla de dos columnas— sin invertir en
 * un encabezado de página propio: «Nueva conversación» y la ayuda ya viven adentro
 * del panel.
 *
 * La conversación es de la página y no del modal: dos montajes, dos hilos. Navegar
 * a otra pantalla la desmonta, y con ella se aborta el turno en vuelo.
 */
export function AsistentePage() {
  const asistente = useAsistente();
  // Habilitado siempre: a diferencia del modal, esta página no tiene un
  // estado de «cerrada» — mientras está montada, está a la vista.
  const historial = useHistorialAsistente(asistente, true);

  return (
    <div className="adoc-asistente-pagina">
      <PageHeader
        title="Asistente"
        meta="Consultá en lenguaje natural lo que ya podés ver en el sistema"
      />
      <PanelAsistente asistente={asistente} historial={historial} />
    </div>
  );
}
