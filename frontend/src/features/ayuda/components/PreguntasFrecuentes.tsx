import type { PreguntaAyuda } from "../tipos";
import { ID_PREGUNTAS_FRECUENTES } from "../utils/rutas";

/** Acordeón de preguntas frecuentes (`<details>` nativo: teclado y lectores de pantalla). */
export function PreguntasFrecuentes({ preguntas }: { preguntas: readonly PreguntaAyuda[] }) {
  return (
    <section id={ID_PREGUNTAS_FRECUENTES} className="ayuda-seccion">
      <h2>Preguntas frecuentes</h2>
      {preguntas.map((item) => (
        <details key={item.pregunta} className="ayuda-pregunta">
          <summary>{item.pregunta}</summary>
          <p>{item.respuesta}</p>
        </details>
      ))}
    </section>
  );
}
