import { PageHeader } from "../../../shared/ui/PageHeader";
import { AbrirHistorial } from "../components/AbrirHistorial";
import { AyudaDelAsistente } from "../components/AyudaDelAsistente";
import { NuevaConversacion } from "../components/NuevaConversacion";
import { PanelAsistente } from "../components/PanelAsistente";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAsistente } from "../hooks/useAsistente";
import { useHistorialAsistente } from "../hooks/useHistorialAsistente";
import "../asistente.css";

/**
 * El asistente a página completa. El otro montaje es el modal de la barra superior.
 *
 * La conversación es de la página y no del modal: dos montajes, dos hilos. Navegar
 * a otra pantalla la desmonta, y con ella se aborta el turno en vuelo.
 *
 * La columna tiene alto fijo para que scrollee el hilo y no `.adoc-main` entero:
 * con la página scrolleando, el campo de entrada bajaba con cada respuesta y había
 * que perseguirlo, que es el defecto que el modal ya no tiene. El encabezado va
 * adentro de la columna para que tome su alto natural sin que haya que medirlo.
 */
export function AsistentePage() {
  const { tieneAcceso } = useAccesoAlAsistente();
  const asistente = useAsistente();
  const historial = useHistorialAsistente(asistente);

  return (
    <div className="adoc-asistente-pagina">
      <PageHeader
        title="Asistente"
        meta="Consultá en lenguaje natural lo que ya podés ver en el sistema"
        // Sin acceso el panel muestra sólo el aviso, y un botón al lado que nunca va
        // a poder hacer nada es la promesa vacía que el invariante #7 prohíbe.
        actions={
          tieneAcceso === true ? (
            <div className="adoc-asistente-acciones-pagina">
              <AyudaDelAsistente />
              <AbrirHistorial historial={historial} />
              <NuevaConversacion asistente={asistente} />
            </div>
          ) : undefined
        }
      />
      <PanelAsistente asistente={asistente} historial={historial} />
    </div>
  );
}
