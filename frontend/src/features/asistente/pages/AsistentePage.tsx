import { PageHeader } from "../../../shared/ui/PageHeader";
import { PanelAsistente } from "../components/PanelAsistente";
import "../asistente.css";

/** El asistente a página completa. El otro montaje es el cajón de la barra superior. */
export function AsistentePage() {
  return (
    <>
      <PageHeader
        title="Asistente"
        meta="Consultá en lenguaje natural lo que ya podés ver en el sistema"
      />
      <PanelAsistente />
    </>
  );
}
