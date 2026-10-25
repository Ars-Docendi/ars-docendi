import type { RefObject } from "react";

import type { CredencialDeEmpresa } from "../types";
import { CampoDeClaveDeEmpresa } from "./CampoDeClaveDeEmpresa";
import { ListaDeModelosEnLaNube, type GrupoDeModelosEnLaNube } from "./ListaDeModelosEnLaNube";
import { PanelDeReemplazoDeClave } from "./PanelDeReemplazoDeClave";

export interface ReemplazoEnCurso {
  validando: boolean;
  error: string | null;
}

interface PanelDeModeloEnLaNubeProps {
  grupos: GrupoDeModelosEnLaNube[];
  seleccion: { empresa: string; modelo: string } | null;
  enUso: { empresa: string; modelo: string } | null;
  onElegir: (empresa: string, modelo: string) => void;
  cifradoDisponible: boolean;
  claveNueva: string;
  onClaveNueva: (clave: string) => void;
  /** El panel de reemplazo de la empresa elegida, o `null` si está cerrado. */
  reemplazo: ReemplazoEnCurso | null;
  onReemplazar: () => void;
  onConfirmarReemplazo: (clave: string) => void;
  onCancelarReemplazo: () => void;
  botonReemplazarRef: RefObject<HTMLButtonElement | null>;
}

/**
 * La configuración de una API en la nube (tareas 4.6, 4.7 y 4.9): la lista
 * agrupada por empresa y, a su lado, la clave de la empresa elegida. El panel
 * de reemplazo ocupa una fila propia debajo.
 */
export function PanelDeModeloEnLaNube({
  grupos,
  seleccion,
  enUso,
  onElegir,
  cifradoDisponible,
  claveNueva,
  onClaveNueva,
  reemplazo,
  onReemplazar,
  onConfirmarReemplazo,
  onCancelarReemplazo,
  botonReemplazarRef,
}: PanelDeModeloEnLaNubeProps) {
  const grupo = grupos.find((g) => g.empresa === seleccion?.empresa);
  const credencial: CredencialDeEmpresa | null = grupo?.credencial ?? null;

  return (
    <>
      <div className="adoc-asistente-modelo-campos">
        <div className="adoc-field">
          <span className="adoc-label" id="modelo-nube-etiqueta">
            Modelo
            <span className="req" aria-hidden="true">
              *
            </span>
          </span>
          <ListaDeModelosEnLaNube
            id="modelo-nube"
            etiquetadaPor="modelo-nube-etiqueta"
            describedBy="modelo-nube-pista"
            grupos={grupos}
            seleccion={seleccion}
            enUso={enUso}
            onElegir={onElegir}
          />
          <div id="modelo-nube-pista" className="adoc-hint">
            Agrupados por empresa. Cada empresa usa una sola clave para todos sus modelos.
          </div>
        </div>
        {grupo && (
          <CampoDeClaveDeEmpresa
            nombreDeLaEmpresa={grupo.nombre}
            credencial={credencial}
            cifradoDisponible={cifradoDisponible}
            claveNueva={claveNueva}
            onClaveNueva={onClaveNueva}
            reemplazando={reemplazo !== null}
            onReemplazar={onReemplazar}
            botonReemplazarRef={botonReemplazarRef}
          />
        )}
      </div>
      {grupo && reemplazo && credencial?.pista && (
        <PanelDeReemplazoDeClave
          nombreDeLaEmpresa={grupo.nombre}
          pista={credencial.pista}
          validando={reemplazo.validando}
          error={reemplazo.error}
          onConfirmar={onConfirmarReemplazo}
          onCancelar={onCancelarReemplazo}
        />
      )}
    </>
  );
}
