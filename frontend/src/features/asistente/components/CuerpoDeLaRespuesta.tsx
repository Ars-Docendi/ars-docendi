import { useRef, useState } from "react";
import { InlineAlert } from "@ars-docendi/ui";

import { BarraDeAcciones } from "./BarraDeAcciones";
import { FichaDelElemento } from "./FichaDelElemento";
import { Opciones } from "./Opciones";
import { ProcedenciaDeLosDatos } from "./ProcedenciaDeLosDatos";
import { Razonamiento } from "./Razonamiento";
import { TablaDeResultado } from "./TablaDeResultado";
import { TextoConVinculos } from "./TextoConVinculos";
import { tipoConFicha } from "../utils/destinos";
import type { EstadoDelTurno, RespuestaDelAsistente } from "../types";

interface CuerpoDeLaRespuestaProps {
  respuesta: RespuestaDelAsistente;
  /** La pregunta del turno, para el título de la tabla ampliada. */
  pregunta: string;
  onElegir: (pregunta: string) => void;
  enVuelo: boolean;
  /** Modo debug: ver `Mensaje`. */
  debug: boolean;
  /** Mientras se edita la pregunta, la respuesta se atenúa. */
  editando: boolean;
}

/**
 * Todo lo que contesta a una pregunta, dentro de UN bloque con fondo elevado y
 * borde (asistente-respuestas-con-vinculos, D7): texto, ficha o tabla, opciones,
 * pie, procedencia y barra de acciones.
 *
 * Vive fuera de `Mensaje` (D8) para que éste no crezca. El estado de la vista
 * ampliada y de la exportación se levantó hasta acá, el ancestro común de la
 * tabla y de la barra de acciones.
 */
export function CuerpoDeLaRespuesta({
  respuesta,
  pregunta,
  onElegir,
  enVuelo,
  debug,
  editando,
}: CuerpoDeLaRespuestaProps) {
  // La vista ampliada de la tabla es controlada desde ACÁ, no desde
  // `TablaDeResultado` (design.md D6 de asistente-rediseno-v3, ARS-146 §5): el
  // disparador vive en `BarraDeAcciones`, así que el estado tiene que vivir en
  // el ancestro común de las dos. `onExportar` es la función YA resuelta que
  // `TablaDeResultado` expone por el mismo motivo — exportar sin duplicar el
  // orden mostrado (asistente-exportacion-csv).
  const [ampliado, setAmpliado] = useState(false);
  const [exportar, setExportar] = useState<(() => void) | null>(null);
  // A dónde vuelve el foco cuando la vista ampliada se contrae desde adentro
  // (Escape o «Contraer»): el disparador es un ícono de `BarraDeAcciones`, no
  // el botón propio que `TablaDeResultado` deja de dibujar en modo controlado.
  const ampliarBotonRef = useRef<HTMLButtonElement>(null);

  function alCambiarAmpliado(valor: boolean) {
    setAmpliado(valor);
    if (!valor) ampliarBotonRef.current?.focus();
  }

  // CON FICHA, la ficha lleva el enlace y el texto no lleva chips; la tabla de
  // una fila no se repite debajo pero sigue montada —oculta— para que «Ampliar
  // tabla» y «Exportar a CSV» sigan funcionando (D3). Un principal de un tipo que
  // este cliente no conoce se ignora: la respuesta se ve como si no lo tuviera.
  const principal =
    respuesta.principal && tipoConFicha(respuesta.principal.tipo) ? respuesta.principal : null;

  return (
    <div
      className={
        editando
          ? "adoc-asistente-respuesta adoc-asistente-respuesta--editando"
          : "adoc-asistente-respuesta"
      }
    >
      <span className="adoc-asistente-quien">Asistente:</span>

      {respuesta.preguntaInterpretada && (
        // Solo llega cuando difiere de lo que se escribió, así que mostrarla
        // nunca es ruido: es el asistente diciendo cómo entendió la pregunta.
        // Queda VISIBLE y fuera de la disclosure del razonamiento: esconder el
        // aviso de que se reinterpretó la pregunta derrota su razón de ser.
        <p className="adoc-asistente-interpretada">
          Entendí: <em>{respuesta.preguntaInterpretada}</em>
        </p>
      )}

      <MarcoDelEstado estado={respuesta.estado}>
        <TextoConVinculos
          texto={respuesta.respuesta}
          vinculos={principal ? [] : (respuesta.vinculosDelTexto ?? [])}
        />
      </MarcoDelEstado>

      {principal && <FichaDelElemento principal={principal} />}

      <TablaDeResultado
        columnas={respuesta.columnas}
        filas={respuesta.filas}
        truncado={respuesta.truncado}
        vinculos={respuesta.vinculos}
        hilo={respuesta.hilo}
        pregunta={pregunta}
        soloAmpliada={principal !== null}
        ampliado={ampliado}
        onAmpliarChange={alCambiarAmpliado}
        // `setExportar(fn)` A SECAS, NO: `useState` interpreta un valor
        // función como la forma "actualizador" (`(prev) => next`) y la
        // LLAMARÍA en vez de guardarla, dejando `exportar` en lo que sea
        // que esa llamada devuelva (nada: `exportar` no devuelve nada).
        // Envolverla en otra función es lo que hace que se guarde la
        // función en sí.
        onExportarDisponible={(fn) => setExportar(() => fn)}
      />

      <Opciones opciones={respuesta.opciones} onElegir={onElegir} deshabilitado={enVuelo} />

      {((debug && respuesta.razonamiento) || respuesta.sql) && (
        // El pie: lo que se puede desplegar a pedido, después de todo lo que
        // hay que leer. Sólo existe cuando hay algo que poner: un pie vacío
        // dejaría un hueco. Con el modo debug apagado, el razonamiento no
        // cuenta como «algo que poner» aunque el backend lo haya mandado.
        // La barra de acciones YA NO vive acá (design.md D6 de
        // asistente-rediseno-v3): sus propias condiciones —portapapeles,
        // tabla, token— no tienen nada que ver con el razonamiento o la
        // consulta, así que depender de este mismo `if` la escondía sin
        // portapapeles aunque hubiera tabla o voto para ofrecer.
        <div className="adoc-asistente-pie">
          <Razonamiento razonamiento={respuesta.razonamiento} debug={debug} />

          {respuesta.sql && (
            // Solo llega con `asistente.ver_consulta`. Que esté acá no es
            // transparencia gratuita: el WHERE de una consulta generada puede
            // llevar un documento, y por eso quien la ve pasó por un permiso.
            <details className="adoc-asistente-sql">
              <summary>Ver la consulta</summary>
              <pre>{respuesta.sql}</pre>
            </details>
          )}
        </div>
      )}

      <ProcedenciaDeLosDatos origenes={respuesta.origenes ?? []} />

      <BarraDeAcciones
        texto={respuesta.respuesta}
        onExportar={exportar}
        onAmpliar={() => alCambiarAmpliado(true)}
        ampliarBotonRef={ampliarBotonRef}
        claveDeRetroalimentacion={respuesta.claveDeRetroalimentacion}
      />
    </div>
  );
}

/**
 * Envuelve el texto según el estado.
 *
 * EL DEGRADADO SE MUESTRA COMO ESTADO Y NO COMO ERROR. Un banner rojo le dice al
 * usuario que hizo algo mal; el servicio degradado no es culpa suya y su pregunta no
 * tiene nada de malo. La aclaración tampoco es un rechazo: es «puedo, en cuanto
 * elijas».
 */
function MarcoDelEstado({
  estado,
  children,
}: {
  estado: EstadoDelTurno;
  children: React.ReactNode;
}) {
  if (estado === "servicio_degradado") {
    return (
      <InlineAlert severity="warning" title="El asistente no está disponible ahora">
        {children}
      </InlineAlert>
    );
  }

  if (estado === "necesita_aclaracion") {
    return (
      <InlineAlert severity="info" title="Necesito que precises algo">
        {children}
      </InlineAlert>
    );
  }

  return <>{children}</>;
}
