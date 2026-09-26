import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { Button } from "@ars-docendi/ui";

import { TablaOrdenable } from "./TablaDeResultado";
import { contraerTablaIcon, copyIcon, downloadIcon } from "../../../app/shell/icons";
import { descargarArchivo } from "../utils/descargas";
import type { OrdenDeColumna } from "../utils/ordenarFilas";
import {
  copiar,
  hayPortapapeles,
  nombreDelArchivoCsv,
  tablaComoCsv,
  tablaComoTsv,
} from "../utils/portapapeles";
import type { ColumnaDelResultado, VinculoDelResultado } from "../types";

/** Cuánto dura «Copiado» antes de volver a «Copiar tabla». Igual que `BarraDeAcciones`. */
const DURACION_DEL_COPIADO_MS = 2000;

interface TablaAmpliadaProps {
  /** La pregunta del turno: título de esta vista. */
  pregunta: string;
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  ordenDeFilas: number[];
  orden: OrdenDeColumna | null;
  onOrdenar: (indiceDeColumna: number) => void;
  porCelda: Map<string, VinculoDelResultado>;
  truncado: boolean;
  haySensibles: boolean;
  /** El hilo del turno, sólo para nombrar el archivo exportado. */
  hilo: string;
  /** Cierra esta capa. NUNCA cierra el modal (asistente-tabla-de-resultado). */
  onContraer: () => void;
}

/**
 * «Tabla ampliada»: la misma tabla, cubriendo todo el modal
 * (asistente-tabla-de-resultado). `TablaDeResultado` la monta vía un
 * `createPortal` dentro de `.adoc-asistente-grilla` — ver su comentario en
 * `contenedorDelModal` para el por qué del portal.
 *
 * COMPARTE EL ORDEN CON LA VISTA EN LÍNEA: `orden`/`onOrdenar` llegan por
 * props desde el mismo estado que ya usa `TablaDeResultado`, así que ordenar
 * acá o allá es ordenar LO MISMO — nunca una copia.
 */
export function TablaAmpliada({
  pregunta,
  columnas,
  filas,
  ordenDeFilas,
  orden,
  onOrdenar,
  porCelda,
  truncado,
  haySensibles,
  hilo,
  onContraer,
}: TablaAmpliadaProps) {
  const contraerRef = useRef<HTMLButtonElement>(null);
  const [copiado, setCopiado] = useState(false);
  const [confirmacionDeExportar, setConfirmacionDeExportar] = useState<string | null>(null);
  const temporizadorDeCopiado = useRef<number | undefined>(undefined);

  // Al abrir, el foco entra a esta capa — quedarse en el disparador de afuera
  // lo dejaría bajo una capa que lo tapa visualmente.
  useEffect(() => {
    contraerRef.current?.focus();
  }, []);

  useEffect(() => () => window.clearTimeout(temporizadorDeCopiado.current), []);

  function filasOrdenadas(): unknown[][] {
    return ordenDeFilas.map((indice) => filas[indice]);
  }

  async function copiarTabla() {
    try {
      await copiar(tablaComoTsv(columnas, filasOrdenadas()));
    } catch {
      // El navegador lo negó. El texto sigue en la tabla para seleccionarlo a
      // mano; la etiqueta no miente diciendo «Copiado».
      return;
    }

    setCopiado(true);
    window.clearTimeout(temporizadorDeCopiado.current);
    temporizadorDeCopiado.current = window.setTimeout(
      () => setCopiado(false),
      DURACION_DEL_COPIADO_MS,
    );
  }

  function exportar() {
    const csv = tablaComoCsv(columnas, filasOrdenadas(), truncado);
    const nombre = nombreDelArchivoCsv(hilo, new Date(), truncado);

    descargarArchivo(nombre, csv, "text/csv;charset=utf-8");
    setConfirmacionDeExportar("El archivo está listo para descargar.");
  }

  function alTeclear(evento: KeyboardEvent<HTMLDivElement>) {
    if (evento.key !== "Escape") return;

    // CAPTURA, no burbujeo (design.md D5 de asistente-rediseno-v3): el Modal
    // de @ars-docendi/ui escucha Escape en `window` sin mirar si el evento ya
    // fue atendido más adentro, así que cerraría el modal ENTERO en vez de
    // sólo esta capa. Frenarlo en la fase de captura —antes de que el propio
    // evento termine de llegar a su objetivo— es lo único que evita que
    // también dispare esa escucha, que corre después, en el burbujeo.
    evento.stopPropagation();
    onContraer();
  }

  return (
    <div
      className="adoc-asistente-ampliada"
      role="region"
      aria-label="Tabla ampliada"
      onKeyDownCapture={alTeclear}
    >
      <header className="adoc-asistente-ampliada-encabezado">
        <div className="adoc-asistente-ampliada-titulo">
          <span className="adoc-asistente-ampliada-eyebrow">Tabla ampliada</span>
          <span className="adoc-asistente-ampliada-pregunta">{pregunta}</span>
        </div>

        {hayPortapapeles() && (
          <Button
            variant="ghost"
            size="sm"
            leadingIcon={copyIcon}
            onClick={() => void copiarTabla()}
          >
            {copiado ? "Copiado" : "Copiar tabla"}
          </Button>
        )}

        <Button variant="ghost" size="sm" leadingIcon={downloadIcon} onClick={exportar}>
          Exportar a CSV
        </Button>

        <Button
          ref={contraerRef}
          variant="secondary"
          size="sm"
          leadingIcon={contraerTablaIcon}
          onClick={onContraer}
        >
          Contraer
        </Button>
      </header>

      <div className="adoc-asistente-ampliada-cuerpo">
        <TablaOrdenable
          columnas={columnas}
          filas={filas}
          ordenDeFilas={ordenDeFilas}
          orden={orden}
          onOrdenar={onOrdenar}
          porCelda={porCelda}
        />

        {haySensibles && (
          <p className="adoc-asistente-leyenda-sensible">
            Las columnas con candado contienen datos personales.
          </p>
        )}

        {truncado && (
          <p className="adoc-asistente-truncado">
            Hay más resultados de los que se muestran. Acotá la pregunta para verlos.
          </p>
        )}

        {confirmacionDeExportar && (
          <p className="adoc-asistente-exportar-confirmacion">{confirmacionDeExportar}</p>
        )}
      </div>
    </div>
  );
}
