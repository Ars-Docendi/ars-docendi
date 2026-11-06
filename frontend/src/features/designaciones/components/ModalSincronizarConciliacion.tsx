import { Button, InlineAlert } from "@ars-docendi/ui";
import { Modal } from "../../../shared/ui/Modal";
import {
  lineaPreview,
  nombreCompleto,
  pendientes,
  plural,
  resumenSincronizacion,
  type DocenteConciliacion,
} from "./conciliacionModelo";

interface ModalSincronizarConciliacionProps {
  aceptados: DocenteConciliacion[];
  sincronizando: boolean;
  error?: string;
  onCancelar: () => void;
  onConfirmar: () => void;
}

/** Preview de lo que se escribe en la base antes de sincronizar. */
export function ModalSincronizarConciliacion({
  aceptados,
  sincronizando,
  error,
  onCancelar,
  onConfirmar,
}: ModalSincronizarConciliacionProps) {
  const resumen = resumenSincronizacion(aceptados);
  const totales = [
    { n: resumen.personasNuevas, t: "Docentes nuevos" },
    { n: resumen.altas, t: "Altas" },
    { n: resumen.cambios, t: "Cambios" },
    { n: resumen.bajas, t: "Bajas" },
  ];
  const cantidad = resumen.pedidos.length;

  return (
    <Modal
      open
      tamano="libre"
      className="conc-modal-ancho"
      title="Sincronizar con la base"
      pendiente={sincronizando}
      onOpenChange={(abierto) => !abierto && onCancelar()}
      footer={
        <>
          <Button variant="secondary" onClick={onCancelar} disabled={sincronizando}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={onConfirmar} loading={sincronizando}>
            Sincronizar {plural(cantidad, "cambio", "cambios")}
          </Button>
        </>
      }
    >
      <div className="conc-modal-cuerpo">
        {error && (
          <InlineAlert severity="danger" title="No se sincronizó nada">
            {error}
          </InlineAlert>
        )}
        <div className="conc-preview-totales">
          {totales.map(({ n, t }) => (
            <div key={t} className="conc-total">
              <div className="n">{n}</div>
              <div className="t">{t}</div>
            </div>
          ))}
        </div>
        <div className="conc-preview-lista">
          {aceptados.map((docente) => (
            <div key={docente.personaId} className="conc-preview-doc">
              <div className="nom">
                {nombreCompleto(docente)}
                {docente.personaNueva && (
                  <span className="conc-chip-persona-nueva">Primera designación</span>
                )}
              </div>
              <ul>
                {pendientes(docente).map((pedido) => (
                  <li key={pedido.id}>{lineaPreview(pedido)}</li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <p className="conc-preview-nota">
          Al sincronizar se actualiza la base de docentes: se escriben las altas, los cambios de
          cargo o dedicación y las bajas de cada materia, y el rol Docente se asigna en la materia
          de cada alta y se retira en la de cada baja. Quien todavía no tiene mail recibe el rol
          cuando se lo cargan en Personas. Los pedidos pasan a Aprobado y ya no se pueden editar.
        </p>
      </div>
    </Modal>
  );
}
