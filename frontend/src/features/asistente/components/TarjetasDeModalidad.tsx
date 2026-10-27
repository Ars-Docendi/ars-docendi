import { Radio } from "@ars-docendi/ui";

export type ModalidadDelModelo = "local" | "nube";

export interface TarjetaDeModalidad {
  id: ModalidadDelModelo;
  nombre: string;
  descripcion: string;
  nota: string;
  enUso: boolean;
  /** Sin nada que elegir (p. ej. ningún servidor configurado) la tarjeta no se puede marcar. */
  deshabilitada: boolean;
}

interface TarjetasDeModalidadProps {
  tarjetas: TarjetaDeModalidad[];
  modalidad: ModalidadDelModelo;
  onElegir: (modalidad: ModalidadDelModelo) => void;
}

/** «Modelo local» / «API en la nube» (tarea 4.4): dos tarjetas que son un solo grupo de radio. */
export function TarjetasDeModalidad({ tarjetas, modalidad, onElegir }: TarjetasDeModalidadProps) {
  return (
    <div
      className="adoc-asistente-modelo-modalidades"
      role="radiogroup"
      aria-label="Dónde corre el modelo"
    >
      {tarjetas.map((t) => {
        const activa = modalidad === t.id;
        return (
          <div
            key={t.id}
            className={`sistema-tarjeta adoc-asistente-modelo-tarjeta${
              activa ? " adoc-asistente-modelo-tarjeta--activa" : ""
            }`}
            onClick={() => !t.deshabilitada && onElegir(t.id)}
          >
            <span className="sistema-tarjeta-encabezado">
              <Radio
                name="modalidad-del-modelo"
                value={t.id}
                checked={activa}
                disabled={t.deshabilitada}
                onChange={() => onElegir(t.id)}
                aria-describedby={`modalidad-${t.id}-descripcion`}
                label={<span className="sistema-tarjeta-nombre">{t.nombre}</span>}
              />
              {t.enUso && (
                <span className="sistema-pill sistema-pill--disponible">
                  <span aria-hidden="true" className="sistema-pill-punto" />
                  En uso
                </span>
              )}
            </span>
            <span id={`modalidad-${t.id}-descripcion`} className="sistema-tarjeta-nota">
              {t.descripcion}
            </span>
            <span className="sistema-tarjeta-nota">{t.nota}</span>
          </div>
        );
      })}
    </div>
  );
}
