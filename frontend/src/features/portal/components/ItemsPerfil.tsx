import { TrafficLight } from "@ars-docendi/ui";

import { IconoExternalLink, IconoFileText } from "../../../shared/ui/iconos";
import type { Certificacion, Educacion, Experiencia, Proyecto } from "../types";
import { rangoPeriodo, vencimientoDeCertificacion } from "../formato";
import "./portal.css";

export function ItemExperiencia({ item }: { item: Experiencia }) {
  return (
    <>
      <span className="portal-item-titulo">
        {item.puesto} · {item.organizacion}
      </span>
      <span className="portal-item-detalle">{rangoPeriodo(item)}</span>
      <span className="portal-item-nota">{item.descripcion}</span>
    </>
  );
}

export function ItemEducacion({ item }: { item: Educacion }) {
  return (
    <>
      <span className="portal-item-titulo">
        {item.nivel} · {item.carrera}
      </span>
      <span className="portal-item-detalle">
        {item.institucion} · {rangoPeriodo(item)}
      </span>
    </>
  );
}

export function ItemCertificacion({ item }: { item: Certificacion }) {
  const vencimiento = vencimientoDeCertificacion(item.vencimiento);
  return (
    <>
      <span className="portal-item-titulo">{item.nombre}</span>
      <span className="portal-item-detalle">
        {item.emisor} · {item.fecha}
      </span>
      {vencimiento && (
        <span className="portal-item-enlaces">
          <TrafficLight state={vencimiento.estado} due={vencimiento.detalle} />
        </span>
      )}
    </>
  );
}

export function ItemProyecto({ item }: { item: Proyecto }) {
  return (
    <>
      <span className="portal-item-titulo">{item.nombre}</span>
      <span className="portal-item-detalle">
        {item.rol} · {rangoPeriodo(item)}
      </span>
      <span className="portal-item-nota">{item.descripcion}</span>
      {(item.doi || item.documento) && (
        <span className="portal-item-enlaces">
          {item.doi && (
            <span className="portal-item-enlace">
              <IconoExternalLink />
              {item.doi}
            </span>
          )}
          {item.documento && (
            <span className="portal-item-enlace">
              <IconoFileText />
              {item.documento.nombre}
            </span>
          )}
        </span>
      )}
    </>
  );
}
