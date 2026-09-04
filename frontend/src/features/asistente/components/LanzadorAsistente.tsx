import { useEffect, useRef, useState } from "react";
import { Modal } from "@ars-docendi/ui";

import { PanelAsistente } from "./PanelAsistente";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAsistente } from "../hooks/useAsistente";
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
 *
 * LA CONVERSACIÓN VIVE ACÁ, NO EN EL PANEL. El panel se monta al abrir y se
 * desmonta al cerrar, y Esc o un clic afuera —también sin querer— cierran: con el
 * hilo en el panel, un clic fuera lo tiraba. El lanzador vive con la barra, así que
 * al reabrir la conversación sigue donde estaba, y un turno en vuelo al cerrar
 * llega igual y espera. No hay ningún pedido al backend hasta la primera pregunta y
 * nada se guarda en el navegador. La ruta tiene la suya: son dos hilos.
 */
export function LanzadorAsistente() {
  const { tieneAcceso } = useAccesoAlAsistente();
  const [abierto, setAbierto] = useState(false);
  const asistente = useAsistente();
  const lanzador = useRef<HTMLButtonElement>(null);

  // EL MODAL DE LA LIBRERÍA NO GESTIONA EL FOCO: ni lo contiene ni lo devuelve. Se
  // portalea a `body`, hermano de `#root`, así que hacer inerte la raíz mientras
  // está abierto deja Tab contenido en el diálogo sin implementar un trap a mano.
  // Al cerrar, primero se restaura la raíz y después vuelve el foco al lanzador: un
  // elemento dentro de un subárbol inerte no se puede enfocar, así que el orden
  // importa. Cuando el Modal traiga lo suyo, esto sobra (está en tech-debt).
  useEffect(() => {
    if (!abierto) return;

    const raiz = document.getElementById("root");
    const boton = lanzador.current;
    raiz?.setAttribute("inert", "");

    return () => {
      raiz?.removeAttribute("inert");
      boton?.focus();
    };
  }, [abierto]);

  if (tieneAcceso !== true) return null;

  return (
    <>
      <button
        ref={lanzador}
        type="button"
        className="adoc-asistente-lanzador"
        onClick={() => setAbierto(true)}
      >
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
        {/* Es sólo la vista: la conversación está arriba, y por eso cerrar no la
            pierde. Con encabezado propio, porque acá no hay encabezado de página. */}
        <PanelAsistente asistente={asistente} mostrarEncabezado />
      </Modal>
    </>
  );
}
