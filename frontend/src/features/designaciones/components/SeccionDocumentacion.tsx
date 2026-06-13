import { useState } from "react";
import { FileUpload, type UploadedFile } from "@ars-docendi/ui";
import {
  TAMANO_MAX_BYTES,
  excedeTamanoMaximo,
  exigeDocumentacion,
  type ArchivoCargado,
  type DocumentacionPedido,
  type TipoPedido,
} from "../mock/mockPedido";

/** Slots de documentos obligatorios (clave en DocumentacionPedido). */
type SlotObligatorio = "cv" | "dniFrente" | "dniDorso";

/** Límite expresado en MB, derivado de la constante para no desincronizar el copy. */
const TAMANO_MAX_MB = Math.round(TAMANO_MAX_BYTES / (1024 * 1024));
const MENSAJE_EXCEDE = `El archivo supera el límite de ${TAMANO_MAX_MB} MB.`;

interface SeccionDocumentacionProps {
  tipo: TipoPedido;
  documentacion: DocumentacionPedido;
  onSetDoc: (slot: SlotObligatorio, archivo: ArchivoCargado | null) => void;
  onAddOtros: (archivos: ArchivoCargado[]) => void;
  onRemoveOtro: (id: string) => void;
}

function formatearTamano(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function archivoDesdeFile(file: File, id: string): ArchivoCargado {
  return { id, name: file.name, size: formatearTamano(file.size) };
}

function comoUploaded(archivo: ArchivoCargado | null): UploadedFile[] {
  return archivo ? [{ id: archivo.id, name: archivo.name, size: archivo.size }] : [];
}

export function SeccionDocumentacion({
  tipo,
  documentacion,
  onSetDoc,
  onAddOtros,
  onRemoveOtro,
}: SeccionDocumentacionProps) {
  const altaNueva = exigeDocumentacion(tipo);
  // Mensaje de error de tamaño por slot. El archivo rechazado no entra al modelo;
  // este estado es feedback transitorio de UI, por eso vive local a la sección.
  const [erroresTamano, setErroresTamano] = useState<Record<string, string | null>>({});

  function setError(slot: string, mensaje: string | null) {
    setErroresTamano((previos) => ({ ...previos, [slot]: mensaje }));
  }

  function handleSlot(slot: SlotObligatorio, files: FileList) {
    const file = files.item(0);
    if (!file) return;
    if (excedeTamanoMaximo(file)) {
      setError(slot, MENSAJE_EXCEDE);
      return;
    }
    setError(slot, null);
    onSetDoc(slot, archivoDesdeFile(file, slot));
  }

  function handleOtros(files: FileList) {
    const validos: ArchivoCargado[] = [];
    let huboExcedido = false;
    for (const file of Array.from(files)) {
      if (excedeTamanoMaximo(file)) {
        huboExcedido = true;
        continue;
      }
      validos.push(archivoDesdeFile(file, crypto.randomUUID()));
    }
    setError("otros", huboExcedido ? MENSAJE_EXCEDE : null);
    if (validos.length > 0) onAddOtros(validos);
  }

  return (
    <section className={`adoc-form-section${altaNueva ? " requiere-docs" : ""}`} id="docs">
      <header>
        <h3>
          5 · Documentación
          {altaNueva && (
            <span
              style={{
                marginLeft: 10,
                fontFamily: "var(--font-mono)",
                fontSize: 11,
                fontWeight: 500,
              }}
            >
              · obligatoria para alta nueva
            </span>
          )}
        </h3>
        <div className="hint">
          {altaNueva
            ? "Subí el CV en PDF y las dos fotos del DNI. Sin estos archivos no podés enviar el pedido."
            : "Adjuntá cualquier documento adicional que respalde el pedido. Opcional para este tipo."}
        </div>
      </header>
      <div className="body">
        <div className="col-12">
          <div className="pedido-docs-grid">
            <div className={`pedido-doc-slot${altaNueva ? " requerido" : ""}`}>
              <div className="doc-titulo">
                CV (PDF){altaNueva && <span className="req">*</span>}
              </div>
              <FileUpload
                accept="application/pdf"
                title="CV (PDF)"
                hint={erroresTamano.cv ?? `Arrastrar acá o seleccionar — máx. ${TAMANO_MAX_MB} MB`}
                files={comoUploaded(documentacion.cv)}
                error={(altaNueva && !documentacion.cv) || Boolean(erroresTamano.cv)}
                onFilesAdded={(files) => handleSlot("cv", files)}
                onRemove={() => onSetDoc("cv", null)}
              />
            </div>

            <div className={`pedido-doc-slot${altaNueva ? " requerido" : ""}`}>
              <div className="doc-titulo">
                DNI · frente{altaNueva && <span className="req">*</span>}
              </div>
              <FileUpload
                accept="image/*"
                title="DNI · frente"
                hint={erroresTamano.dniFrente ?? `JPG o PNG · máx. ${TAMANO_MAX_MB} MB`}
                files={comoUploaded(documentacion.dniFrente)}
                error={(altaNueva && !documentacion.dniFrente) || Boolean(erroresTamano.dniFrente)}
                onFilesAdded={(files) => handleSlot("dniFrente", files)}
                onRemove={() => onSetDoc("dniFrente", null)}
              />
            </div>

            <div className={`pedido-doc-slot${altaNueva ? " requerido" : ""}`}>
              <div className="doc-titulo">
                DNI · dorso{altaNueva && <span className="req">*</span>}
              </div>
              <FileUpload
                accept="image/*"
                title="DNI · dorso"
                hint={erroresTamano.dniDorso ?? `JPG o PNG · máx. ${TAMANO_MAX_MB} MB`}
                files={comoUploaded(documentacion.dniDorso)}
                error={(altaNueva && !documentacion.dniDorso) || Boolean(erroresTamano.dniDorso)}
                onFilesAdded={(files) => handleSlot("dniDorso", files)}
                onRemove={() => onSetDoc("dniDorso", null)}
              />
            </div>
          </div>
        </div>

        <div className="col-12 pedido-doc-otros">
          <FileUpload
            multiple
            title="Adjuntar otros documentos (opcional)"
            hint={
              erroresTamano.otros ??
              `Plan de trabajo · certificaciones · otros respaldatorios · máx. ${TAMANO_MAX_MB} MB c/u`
            }
            error={Boolean(erroresTamano.otros)}
            files={documentacion.otros.map((a) => ({ id: a.id, name: a.name, size: a.size }))}
            onFilesAdded={handleOtros}
            onRemove={onRemoveOtro}
          />
        </div>
      </div>
    </section>
  );
}
