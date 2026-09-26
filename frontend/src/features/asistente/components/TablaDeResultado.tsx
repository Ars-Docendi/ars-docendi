import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button, Table } from "@ars-docendi/ui";
import { Link } from "react-router-dom";

import { MarcaSensible } from "./MarcaSensible";
import { TablaAmpliada } from "./TablaAmpliada";
import {
  ampliarTablaIcon,
  downloadIcon,
  ordenAscendenteIcon,
  ordenDescendenteIcon,
  ordenNeutroIcon,
} from "../../../app/shell/icons";
import { formatearCelda } from "../utils/celdas";
import { descargarArchivo } from "../utils/descargas";
import { destinoDe } from "../utils/destinos";
import { ordenarFilas } from "../utils/ordenarFilas";
import type { DireccionDeOrden, OrdenDeColumna } from "../utils/ordenarFilas";
import { nombreDelArchivoCsv, tablaComoCsv } from "../utils/portapapeles";
import type { ColumnaDelResultado, VinculoDelResultado } from "../types";

interface TablaDeResultadoProps {
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  truncado: boolean;
  vinculos?: VinculoDelResultado[];
  /** El hilo del turno, sólo para nombrar el archivo exportado. */
  hilo?: string;
  /**
   * La pregunta del turno: título de «Tabla ampliada»
   * (asistente-tabla-de-resultado). Sin ella —un llamador que todavía no la
   * tiene, o un test que no la necesita— la vista ampliada muestra un
   * título vacío en vez de romper.
   */
  pregunta?: string;
  /**
   * La vista ampliada, controlada desde afuera. Pasada junto con
   * `onAmpliarChange`, esta tabla DEJA DE DIBUJAR su propio botón «Ampliar
   * tabla» —el llamador ya tiene el suyo en otro lado— y sigue el valor que
   * le llega; devolver el foco al abrir/cerrar pasa a ser responsabilidad de
   * quien controla. Pensado para cuando la barra de acciones (ARS-146, §5)
   * mueva el disparador a su propio ícono sin tener que editar este
   * archivo. Sin este par —el caso de hoy— la tabla maneja su propio estado
   * y ofrece su propio botón, como siempre.
   */
  ampliado?: boolean;
  onAmpliarChange?: (ampliado: boolean) => void;
  /**
   * Expone la función de exportar cuando la barra de acciones (ARS-146, §5)
   * quiere su propio ícono «Exportar a CSV»: llega YA resuelta contra el
   * orden mostrado (`ordenDeFilas`), la misma fuente que usa la vista en
   * línea y la ampliada —no hay una segunda función que ordene por su cuenta
   * (asistente-exportacion-csv: exportar SIEMPRE el orden mostrado). Pasada,
   * esta tabla DEJA DE DIBUJAR su propio botón «Exportar a CSV», igual que
   * con `ampliado`/`onAmpliarChange`. Sin ella, la tabla exporta con su
   * propio botón, como siempre.
   */
  onExportarDisponible?: (exportar: () => void) => void;
}

/**
 * Las filas del resultado.
 *
 * Con columnas sensibles, la narración deja de ser el vehículo del dato: el modelo
 * redacta el marco («encontré 4 docentes») y el valor real llega por acá, porque
 * nunca viajó al proveedor. Sin esta tabla, una respuesta con datos personales sería
 * un párrafo con marcadores.
 *
 * LA TABLA SCROLLEA DENTRO DE SU PROPIO MARCO. El envoltorio de la librería trae
 * `overflow: hidden` y la tabla `width: 100%`: con más columnas de las que entran,
 * las de la derecha se recortaban sin aviso. La clase propia que recibe el `Table`
 * es lo que `asistente.css` usa para sobreescribirlo, sin `!important` ni fork.
 *
 * LA CELDA QUE IDENTIFICA ALGO ABRIBLE SE VUELVE ENLACE, y no hay una columna
 * «Ver» aparte: en el modal el ancho ya está comprometido y una columna más le
 * roba lugar al dato. El identificador es además lo que el usuario ya iba a copiar,
 * así que la acción queda donde la mano ya estaba.
 *
 * QUÉ CELDAS SON ENLACE NO LO DECIDE ESTE COMPONENTE. Lo decide el backend, contra
 * el módulo dueño del recurso: que una fila esté acá no significa que su pantalla
 * esté abierta para quien pregunta. Sin vínculo, la celda es texto — y el dato se
 * ve igual.
 */
export function TablaDeResultado({
  columnas,
  filas,
  truncado,
  vinculos = [],
  hilo = "",
  pregunta = "",
  ampliado,
  onAmpliarChange,
  onExportarDisponible,
}: TablaDeResultadoProps) {
  if (columnas.length === 0 || filas.length === 0) return null;

  const haySensibles = columnas.some((columna) => columna.sensible);
  const porCelda = new Map(vinculos.map((v) => [`${v.fila}:${v.columna}`, v]));

  return (
    <TablaConAcciones
      columnas={columnas}
      filas={filas}
      truncado={truncado}
      hilo={hilo}
      pregunta={pregunta}
      haySensibles={haySensibles}
      porCelda={porCelda}
      ampliadoControlado={ampliado}
      onAmpliarChange={onAmpliarChange}
      onExportarDisponible={onExportarDisponible}
    />
  );
}

interface TablaConAccionesProps {
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  truncado: boolean;
  hilo: string;
  pregunta: string;
  haySensibles: boolean;
  /** Ver `TablaDeResultadoProps.ampliado`. */
  ampliadoControlado?: boolean;
  onAmpliarChange?: (ampliado: boolean) => void;
  /** Ver `TablaDeResultadoProps.onExportarDisponible`. */
  onExportarDisponible?: (exportar: () => void) => void;
  porCelda: Map<string, VinculoDelResultado>;
}

/**
 * La renderización que el guardia de arriba protege, más el orden, la vista
 * ampliada y la exportación.
 *
 * Separado del guardia sólo porque necesita hooks (design.md D5 de
 * asistente-rediseno-v3: orden y vista ampliada son estado puro de cliente,
 * por turno) — React no permite un hook después de un `return` condicional en
 * el mismo componente.
 *
 * EL ORDEN VIVE ACÁ, NO EN UN ANCESTRO: este componente es el mismo para
 * todo lo que dura la respuesta de un turno, así que es el lugar correcto
 * para «recordar cómo lo dejó el usuario» sin inventar un mapa por id en un
 * hook compartido. Si algún día el turno se reemplaza (asistente-edicion-de-
 * la-ultima-pregunta) y `Mensaje` vuelve a montar este árbol con una key
 * distinta, este estado se resetea solo — es la razón por la que vive tan
 * cerca de la tabla y no más arriba.
 */
function TablaConAcciones({
  columnas,
  filas,
  truncado,
  hilo,
  pregunta,
  haySensibles,
  porCelda,
  ampliadoControlado,
  onAmpliarChange,
  onExportarDisponible,
}: TablaConAccionesProps) {
  const [orden, setOrden] = useState<OrdenDeColumna | null>(null);
  const [ampliadoPropio, setAmpliadoPropio] = useState(false);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);

  // CONTROLADA O NO, SEGÚN SI EL LLAMADOR TRAE `onAmpliarChange`: sin él —el
  // único caso hoy—, esta tabla maneja su propio estado y dibuja su propio
  // botón; con él, sigue el valor de afuera y NO dibuja el suyo, para que la
  // barra de acciones (ARS-146, §5) pueda mover el disparador a su ícono sin
  // tocar este archivo (ver `TablaDeResultadoProps.ampliado`).
  const controlada = onAmpliarChange !== undefined;
  const ampliado = controlada ? (ampliadoControlado ?? false) : ampliadoPropio;
  // Dónde vive la vista ampliada — ver `contenedorDelModal` más abajo. Estado
  // y no una lectura de `raizRef.current` directamente en el render: los
  // refs se leen fuera del render (efectos, manejadores), nunca adentro.
  const [contenedorAmpliada, setContenedorAmpliada] = useState<HTMLElement | null>(null);

  // Ancla para encontrar el contenedor del modal (`.adoc-asistente-grilla`,
  // ver asistente.css) al abrir la vista ampliada — ver la nota en
  // `contenedorDelModal` más abajo.
  const raizRef = useRef<HTMLDivElement>(null);
  // A dónde vuelve el foco al contraer (asistente-tabla-de-resultado:
  // «focus SHALL return to the control that opened it»).
  const disparadorAmpliarRef = useRef<HTMLButtonElement>(null);

  // Corre una sola vez, después del primer commit: el nodo no cambia después
  // —ni este árbol se re-monta con la tabla adentro—, así que no hace falta
  // repetirlo en cada render.
  useEffect(() => {
    setContenedorAmpliada(contenedorDelModal(raizRef.current));
  }, []);

  // CONTENCIÓN DE FOCO: la vista ampliada CUBRE el modal entero (rail, hilo,
  // composer) pero no era la única cosa alcanzable con Tab — sin esto, un
  // Tab bastaba para salir de la capa que se ve encima y caer en un control
  // tapado visualmente (asistente-rediseno-v3, hallazgo de verificación
  // visual). `inert` en cada HERMANO del nodo que el portal va a montar
  // —nunca en el propio `contenedorAmpliada`, que se quedaría inerte él
  // mismo— saca esos hermanos del árbol de foco y de accesibilidad mientras
  // dura, y el cleanup los devuelve exactamente como estaban al cerrar.
  useEffect(() => {
    if (!ampliado || !contenedorAmpliada) return;

    const hermanos = Array.from(contenedorAmpliada.children).filter(
      (nodo) => !nodo.classList.contains("adoc-asistente-ampliada"),
    );
    hermanos.forEach((nodo) => nodo.setAttribute("inert", ""));

    return () => hermanos.forEach((nodo) => nodo.removeAttribute("inert"));
  }, [ampliado, contenedorAmpliada]);

  const ordenDeFilas = useMemo(() => ordenarFilas(filas, orden), [filas, orden]);

  function alOrdenar(indiceDeColumna: number) {
    setOrden((actual) => {
      if (!actual || actual.columna !== indiceDeColumna) {
        return { columna: indiceDeColumna, direccion: "ascendente" };
      }
      const siguiente: DireccionDeOrden =
        actual.direccion === "ascendente" ? "descendente" : "ascendente";
      return { columna: indiceDeColumna, direccion: siguiente };
    });
  }

  function cambiarAmpliado(valor: boolean) {
    if (onAmpliarChange) onAmpliarChange(valor);
    else setAmpliadoPropio(valor);
  }

  function abrirAmpliada() {
    cambiarAmpliado(true);
  }

  function cerrarAmpliada() {
    cambiarAmpliado(false);
    // Sin control externo, el disparador es el botón propio de abajo —el
    // foco vuelve ahí—. Con control externo, quien controla el disparador
    // (fuera de este árbol) es quien decide a dónde vuelve el foco; `.focus()`
    // sobre un ref que nunca se asignó a nada no hace nada.
    disparadorAmpliarRef.current?.focus();
  }

  const exportar = useCallback(() => {
    // Se relee EN EL MOMENTO DE EXPORTAR, con el orden actual: nunca se cachea
    // una copia reordenada de `filas` aparte del array de índices, así que no
    // hay una segunda fuente de verdad que se pueda desincronizar del orden
    // que la tabla muestra (asistente-exportacion-csv).
    const filasOrdenadas = ordenDeFilas.map((indice) => filas[indice]);
    const csv = tablaComoCsv(columnas, filasOrdenadas, truncado);
    const nombre = nombreDelArchivoCsv(hilo, new Date(), truncado);

    descargarArchivo(nombre, csv, "text/csv;charset=utf-8");
    setConfirmacion("El archivo está listo para descargar.");
  }, [columnas, filas, hilo, ordenDeFilas, truncado]);

  // LA MISMA FUNCIÓN, nunca una copia: quien la recibe (la barra de acciones,
  // §5) exporta exactamente lo que esta tabla muestra, sin repetir el cálculo
  // de `ordenDeFilas` ni el armado del CSV en otro archivo.
  useEffect(() => {
    onExportarDisponible?.(exportar);
  }, [exportar, onExportarDisponible]);

  const anuncioDeOrden = textoDelAnuncioDeOrden(orden, columnas);

  return (
    <div className="adoc-asistente-tabla" ref={raizRef}>
      <TablaOrdenable
        columnas={columnas}
        filas={filas}
        ordenDeFilas={ordenDeFilas}
        orden={orden}
        onOrdenar={alOrdenar}
        porCelda={porCelda}
      />

      {/* Anunciado por la región viva ancestral (Conversacion.tsx, role="log"
          aria-live="polite"): el mismo mecanismo que ya usan «Copiado» y la
          confirmación de exportar, sin abrir una región propia. El foco no se
          mueve — sigue en el encabezado que se activó. */}
      {anuncioDeOrden && <p className="adoc-asistente-orden-anuncio">{anuncioDeOrden}</p>}

      {haySensibles && (
        // Dice qué es personal, no por dónde viajó: el enmascaramiento y el
        // proveedor son mecánica interna (RNF-18).
        <p className="adoc-asistente-leyenda-sensible">
          Las columnas con candado contienen datos personales.
        </p>
      )}

      {truncado && (
        // SIN NÚMEROS. «Ves 3 de 124» es un canal de inferencia sobre datos que el
        // usuario no puede ver: por eso el backend devuelve un booleano y nunca un
        // conteo, y la interfaz respeta la misma regla.
        <p className="adoc-asistente-truncado">
          Hay más resultados de los que se muestran. Acotá la pregunta para verlos.
        </p>
      )}

      {/* Vacío y sin renderizar cuando las dos props de control llegaron —los
          dos botones de acá abajo se apagaron y todavía no hay confirmación
          que anunciar—: sin esta guarda, quedaría un `<div>` fantasma con su
          propio margen, aportando un hueco visual que nadie pidió. */}
      {(!controlada || !onExportarDisponible || confirmacion) && (
        <div className="adoc-asistente-exportar">
          <div className="adoc-asistente-exportar-botones">
            {/* Sólo sin control externo: controlada, el disparador ya vive en
                otro lado (la barra de acciones, §5) y uno propio acá sería el
                duplicado que ese grupo tendría que borrar. */}
            {!controlada && (
              <Button
                ref={disparadorAmpliarRef}
                variant="ghost"
                size="sm"
                leadingIcon={ampliarTablaIcon}
                onClick={abrirAmpliada}
              >
                Ampliar tabla
              </Button>
            )}

            {/* Igual que arriba, pero contra `onExportarDisponible`: no hace
                falta que sea el MISMO llamador que trae `onAmpliarChange`
                —cada botón se apaga contra la prop que efectivamente lo
                reemplaza—, aunque hoy los dos siempre llegan juntos desde
                `Mensaje`. */}
            {!onExportarDisponible && (
              <Button variant="ghost" size="sm" leadingIcon={downloadIcon} onClick={exportar}>
                Exportar a CSV
              </Button>
            )}
          </div>

          {/* Anunciado por la región viva ancestral (Conversacion.tsx, role="log"
              aria-live="polite"): no hay una región propia acá, y el foco no se
              mueve — sigue en el botón que se activó. */}
          {confirmacion && <p className="adoc-asistente-exportar-confirmacion">{confirmacion}</p>}
        </div>
      )}

      {ampliado &&
        contenedorAmpliada &&
        createPortal(
          <TablaAmpliada
            pregunta={pregunta}
            columnas={columnas}
            filas={filas}
            ordenDeFilas={ordenDeFilas}
            orden={orden}
            onOrdenar={alOrdenar}
            porCelda={porCelda}
            truncado={truncado}
            haySensibles={haySensibles}
            hilo={hilo}
            onContraer={cerrarAmpliada}
          />,
          contenedorAmpliada,
        )}
    </div>
  );
}

/**
 * Dónde vive la vista ampliada: el mismo `<div>` que ya reparte el rail y la
 * columna de la conversación (`.adoc-asistente-grilla`, `position: relative`
 * en asistente.css) y así, sirviéndose de `position: absolute; inset: 0`
 * sobre ese ancestro, cubre EXACTAMENTE el modal —rail incluido— sin medir
 * nada por JS ni depender de un `Context` que esta feature no usa en ningún
 * otro lado (acá todo se pasa explícito, de dueño a dueño de montaje).
 *
 * `closest` y no un ref pasado por props desde `PanelAsistente`: la cadena
 * hasta acá pasa por `Conversacion` y `Mensaje`, que documentan explícitamente
 * no llevar nada que no rendericen ellos mismos, y este ancla es la única
 * pieza que la vista ampliada necesita de afuera.
 *
 * SIN ESE ANCESTRO —un test que monta `TablaDeResultado` sola, sin el panel
 * alrededor— cae al propio nodo raíz de la tabla: la vista ampliada sigue
 * pintándose (los tests la encuentran igual), sólo que sin la cobertura
 * exacta del modal, que en ese contexto no existe para cubrir.
 */
function contenedorDelModal(raiz: HTMLDivElement | null): HTMLElement {
  return raiz?.closest<HTMLElement>(".adoc-asistente-grilla") ?? raiz ?? document.body;
}

// ============================================================
// La tabla propiamente dicha: encabezados ordenables + filas, en el orden
// mostrado. La usan tanto la vista en línea como la vista ampliada —MISMO
// componente, mismo estado de orden que recibe por props—, así que ordenar
// en una las mantiene sincronizadas sin que ninguna copie a la otra.
// ============================================================

export interface TablaOrdenableProps {
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  /** Los índices originales de `filas`, en el orden a mostrar. */
  ordenDeFilas: number[];
  orden: OrdenDeColumna | null;
  onOrdenar: (indiceDeColumna: number) => void;
  porCelda: Map<string, VinculoDelResultado>;
}

/**
 * La misma clase de marco en las dos vistas: lo que cambia entre la vista en
 * línea (14 px, `max-height: 50vh`) y la ampliada (15 px, sin límite propio)
 * lo distingue `asistente.css` por el ANCESTRO —`.adoc-asistente-tabla` o
 * `.adoc-asistente-ampliada`—, no por una prop de variante acá.
 */
export function TablaOrdenable({
  columnas,
  filas,
  ordenDeFilas,
  orden,
  onOrdenar,
  porCelda,
}: TablaOrdenableProps) {
  return (
    <Table className="adoc-asistente-tabla-wrap">
      <Table.Root>
        <Table.Head>
          <Table.Row>
            {columnas.map((columna, indiceDeColumna) => (
              <EncabezadoOrdenable
                key={columna.nombre}
                columna={columna}
                indiceDeColumna={indiceDeColumna}
                direccion={orden?.columna === indiceDeColumna ? orden.direccion : null}
                onOrdenar={onOrdenar}
              />
            ))}
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {ordenDeFilas.map((indiceOriginal) => (
            <Table.Row key={indiceOriginal}>
              {filas[indiceOriginal].map((valor, indiceDeColumna) => (
                <Table.Cell key={indiceDeColumna} numeric={typeof valor === "number"}>
                  <Celda
                    valor={valor}
                    vinculo={porCelda.get(`${indiceOriginal}:${indiceDeColumna}`)}
                  />
                </Table.Cell>
              ))}
            </Table.Row>
          ))}
        </Table.Body>
      </Table.Root>
    </Table>
  );
}

interface EncabezadoOrdenableProps {
  columna: ColumnaDelResultado;
  indiceDeColumna: number;
  /** `null`: esta columna no es la ordenada ahora. */
  direccion: DireccionDeOrden | null;
  onOrdenar: (indiceDeColumna: number) => void;
}

/**
 * Un encabezado que ordena: botón enfocable con Tab, Enter o Espacio lo
 * activan por ser un `<button>` nativo, y `aria-sort` va en la celda —nunca
 * en el botón— porque es la celda la que tiene el rol `columnheader`
 * (asistente-tabla-de-resultado, asistente-accesibilidad).
 *
 * SIN `aria-sort` EN LAS DEMÁS: la spec lo prohíbe explícitamente —un
 * encabezado sin ordenar no puede reclamar una dirección—, así que se omite
 * el atributo entero en vez de mandar `"none"`.
 */
function EncabezadoOrdenable({
  columna,
  indiceDeColumna,
  direccion,
  onOrdenar,
}: EncabezadoOrdenableProps) {
  return (
    <Table.HeaderCell
      className="adoc-asistente-th-orden"
      aria-sort={
        direccion === "ascendente"
          ? "ascending"
          : direccion === "descendente"
            ? "descending"
            : undefined
      }
    >
      <button
        type="button"
        className="adoc-asistente-orden-boton"
        title={`Ordenar por «${columna.nombre}»`}
        onClick={() => onOrdenar(indiceDeColumna)}
      >
        <span>{columna.nombre}</span>
        {columna.sensible && <MarcaSensible />}
        {/* El nombre accesible del botón dice la columna Y la acción
            (asistente-tabla-de-resultado): lo visible ya dice la columna —y
            el candado, si hay—, así que sólo la acción hace falta agregar,
            oculta a quien ve. */}
        <span className="adoc-sr"> ordenar por esta columna</span>
        <IconoDeOrden direccion={direccion} />
      </button>
    </Table.HeaderCell>
  );
}

function IconoDeOrden({ direccion }: { direccion: DireccionDeOrden | null }) {
  if (direccion === "ascendente") {
    return (
      <span
        className="adoc-asistente-orden-icono adoc-asistente-orden-icono--activo"
        aria-hidden="true"
      >
        {ordenAscendenteIcon}
      </span>
    );
  }

  if (direccion === "descendente") {
    return (
      <span
        className="adoc-asistente-orden-icono adoc-asistente-orden-icono--activo"
        aria-hidden="true"
      >
        {ordenDescendenteIcon}
      </span>
    );
  }

  return (
    <span
      className="adoc-asistente-orden-icono adoc-asistente-orden-icono--inactivo"
      aria-hidden="true"
    >
      {ordenNeutroIcon}
    </span>
  );
}

function textoDelAnuncioDeOrden(
  orden: OrdenDeColumna | null,
  columnas: ColumnaDelResultado[],
): string | null {
  if (!orden) return null;
  const nombre = columnas[orden.columna]?.nombre ?? "";
  const direccion = orden.direccion === "ascendente" ? "ascendente" : "descendente";
  return `Tabla ordenada por «${nombre}», ${direccion}.`;
}

/**
 * El contenido de una celda: texto, o enlace si lleva a algún lado.
 *
 * EL NOMBRE ACCESIBLE DICE A DÓNDE VA. «Enlace, 2026-9005» no informa nada; «Ver el
 * trámite 2026-9005» sí, y es lo único que oye quien no ve la tabla alrededor.
 */
function Celda({ valor, vinculo }: { valor: unknown; vinculo?: VinculoDelResultado }) {
  const texto = formatearCelda(valor);
  const destino = vinculo && destinoDe(vinculo.tipo);

  // Sin destino conocido queda texto. Pasa con un tipo que este cliente todavía no
  // traduce, y es la degradación correcta: el dato se lee igual.
  if (!vinculo || !destino) return <>{texto}</>;

  return (
    <Link
      className="adoc-asistente-vinculo"
      to={destino.ruta(vinculo.id)}
      aria-label={`Ver el ${destino.sustantivo} ${texto}`}
    >
      {texto}
    </Link>
  );
}
