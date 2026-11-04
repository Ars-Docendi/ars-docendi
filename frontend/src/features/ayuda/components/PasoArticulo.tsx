import type { PasoAyuda, TipoNotaAyuda } from "../tipos";
import { idPaso } from "../utils/rutas";
import { VisorImagen } from "./VisorImagen";

const TITULO_NOTA: Record<TipoNotaAyuda, string> = {
  info: "Información",
  importante: "Importante",
  consejo: "Consejo",
};

interface PasoArticuloProps {
  paso: PasoAyuda;
  /** Posición del paso, base 1. */
  numero: number;
}

/** Un paso numerado del artículo, con captura y nota opcionales. */
export function PasoArticulo({ paso, numero }: PasoArticuloProps) {
  const id = idPaso(numero);
  return (
    <section id={id} className="ayuda-paso" aria-labelledby={`${id}-titulo`}>
      <h2 id={`${id}-titulo`}>
        <span className="ayuda-paso-numero" aria-hidden="true">
          {numero}
        </span>
        {paso.titulo}
      </h2>
      <p>{paso.cuerpo}</p>
      {paso.captura && <VisorImagen captura={paso.captura} />}
      {paso.nota && (
        <aside className={`ayuda-nota ${paso.nota.tipo}`} role="note">
          <strong>{paso.nota.titulo ?? TITULO_NOTA[paso.nota.tipo]}</strong>
          <p>{paso.nota.cuerpo}</p>
        </aside>
      )}
    </section>
  );
}
