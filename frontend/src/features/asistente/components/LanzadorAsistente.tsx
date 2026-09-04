import { useState } from "react";
import { Modal } from "@ars-docendi/ui";

import { PanelAsistente } from "./PanelAsistente";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { sparkIcon } from "../../../app/shell/icons";

/**
 * El asistente desde cualquier pantalla.
 *
 * Una ruta a la que hay que navegar NO resuelve el descubrimiento: si el usuario
 * tiene que acordarse de que el asistente existe y buscar dónde está, no lo usa. Por
 * eso hay dos montajes, y éste es el que está siempre a mano.
 *
 * Ocupa el lugar del botón «Ayuda» que estaba `disabled` con `title="Próximamente"`.
 * Activarlo ELIMINA un fake UI existente en vez de agregar superficie nueva, que es
 * lo que el invariante #7 pide.
 *
 * Quien no tiene el permiso no ve nada: ni el botón deshabilitado ni una ruta
 * muerta. El acceso lo decide el backend, no una lista de roles.
 *
 * LLEVA ETIQUETA Y NO SOLO UN ÍCONO. Un ícono solo obliga a descubrir qué hace
 * pasando el mouse por encima, y en la barra ya conviven otros dos; «Preguntar»
 * dice qué pasa al apretarlo sin que haya que averiguarlo.
 *
 * VA EN UN MODAL CENTRADO Y NO EN UN CAJÓN LATERAL. La conversación es la tarea
 * mientras dura: un cajón compite por el ancho con la pantalla que quedó atrás, y a
 * esa pantalla no se la está mirando. El modal también centra el foco del teclado,
 * que es lo que corresponde cuando lo que se abre es donde hay que escribir.
 */
export function LanzadorAsistente() {
  const { tieneAcceso } = useAccesoAlAsistente();
  const [abierto, setAbierto] = useState(false);

  if (tieneAcceso !== true) return null;

  return (
    <>
      <button type="button" className="adoc-asistente-lanzador" onClick={() => setAbierto(true)}>
        <span className="ico">{sparkIcon}</span>
        Preguntar
      </button>

      {/* Con título, el Modal pinta un encabezado que dice qué es esto y nombra el
          diálogo por él; sin título quedaba un encabezado con sólo la «×» y un
          nombre que sólo el lector de pantalla oía. */}
      <Modal
        open={abierto}
        onOpenChange={setAbierto}
        title="Asistente"
        className="adoc-asistente-modal"
      >
        {/* Se monta recién al abrir: el panel arranca una conversación, y montarlo
            siempre dejaría un hilo abierto en cada pantalla de la aplicación. */}
        {abierto && <PanelAsistente />}
      </Modal>
    </>
  );
}
