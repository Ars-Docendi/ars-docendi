import { Link } from "react-router-dom";

import { arrowRightIcon, personIcon } from "../../../app/shell/icons";
import { destinoDe } from "../utils/destinos";
import type { PrincipalDeLaRespuesta } from "../types";

interface TarjetaDeDocenteProps {
  principal: PrincipalDeLaRespuesta;
}

/**
 * La «Tarjeta de docente»: apellido y nombre, sus designaciones vigentes y el enlace a su perfil.
 * Reusa la estructura de la ficha del pedido. El enlace existe porque el backend mandó el
 * principal sólo a quien puede abrir esa persona.
 */
export function TarjetaDeDocente({ principal }: TarjetaDeDocenteProps) {
  const destino = destinoDe(principal.tipo);
  if (!destino) return null;

  const { apellido, nombre, designaciones, email } = principal.datos;
  const lineas = (designaciones ?? "").split("\n").filter((linea) => linea.trim() !== "");

  return (
    <article className="adoc-asistente-ficha">
      <div className="adoc-asistente-ficha-cabecera">
        <span className="adoc-asistente-ficha-icono" aria-hidden="true">
          {personIcon}
        </span>

        <div className="adoc-asistente-ficha-titulos">
          <span className="adoc-asistente-ficha-eyebrow">Docente</span>
          <p className="adoc-asistente-ficha-titulo">
            {apellido}, {nombre}
          </p>
        </div>
      </div>

      {email && (
        <div className="adoc-asistente-ficha-dato">
          <p className="adoc-asistente-ficha-subtitulo">Correo institucional</p>
          <a href={`mailto:${email}`}>{email}</a>
        </div>
      )}

      {lineas.length > 0 && (
        <div className="adoc-asistente-ficha-dato">
          <p className="adoc-asistente-ficha-subtitulo">Designaciones vigentes</p>
          <ul className="adoc-asistente-ficha-lista">
            {lineas.map((linea) => (
              <li key={linea}>{linea}</li>
            ))}
          </ul>
        </div>
      )}

      <div className="adoc-asistente-ficha-pie">
        <Link
          className="adoc-asistente-ficha-abrir"
          to={destino.ruta(principal.id)}
          data-vinculo-del-asistente=""
        >
          Ver perfil
          <span className="adoc-asistente-ficha-flecha" aria-hidden="true">
            {arrowRightIcon}
          </span>
        </Link>
      </div>
    </article>
  );
}
