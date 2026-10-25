import { useEffect, useRef, useState, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, InlineAlert } from "@ars-docendi/ui";

import {
  aplicarModelo,
  cargarPrecio,
  obtenerEstadoDelModelo,
  probarModelo,
  reemplazarClaveDeEmpresa,
  restablecerModelo,
} from "../api/administracionAsistenteApi";
import {
  CODIGO_SATURADO,
  codigoDeError,
  destinoDe,
  esEmpresaEnLaNube,
  esServidorLocal,
  firmaDelPedido,
  gruposEnLaNube,
  modelosDelServidor,
  nombreDeLaEmpresa,
  nombreDelModelo,
  pedidoDe,
  precioDelModelo,
  seleccionInicial,
  textoDelCodigo,
  type SeleccionDelModelo,
} from "../utils/modeloDelAsistente";
import { AccionesDelModelo, type VistaDePrueba } from "./AccionesDelModelo";
import { BannerDelModeloEnUso } from "./BannerDelModeloEnUso";
import type { PedidoDePrecio } from "../types";
import type { GrupoDeModelosEnLaNube } from "./ListaDeModelosEnLaNube";
import { PanelDeModeloEnLaNube, type ReemplazoEnCurso } from "./PanelDeModeloEnLaNube";
import { PanelDeModeloLocal } from "./PanelDeModeloLocal";
import { PrecioDelModelo } from "./PrecioDelModelo";
import { TarjetasDeModalidad } from "./TarjetasDeModalidad";
import "../asistente.css";

/**
 * Todo lo que cambia con el modelo —su estado, y la lista «Cambios del modelo»
 * que arma la sección Sistema— cuelga de este prefijo, así «Actualizar» y las
 * invalidaciones de acá alcanzan a las dos sin que una feature importe a la otra.
 */
const CLAVE_MODELO = ["modelo"] as const;
const CLAVE_ESTADO_DEL_MODELO = ["modelo", "estado"] as const;

interface PanelModeloDelAsistenteProps {
  /**
   * «Cambios del modelo», armado por `features/sistema` (que es quien conoce la
   * auditoría) y compuesto desde `app/router.tsx`. Ausente si el usuario no
   * puede ver la auditoría.
   */
  cambios?: ReactNode;
}

type PruebaGuardada = { firma: string; vista: VistaDePrueba };

function vistaDeFallo(codigo: string, alcance: "local" | "nube"): VistaDePrueba {
  const texto = textoDelCodigo(codigo, alcance);
  return codigo === CODIGO_SATURADO ? { estado: "saturada", texto } : { estado: "fallida", texto };
}

function unirNombres(nombres: string[]): string {
  if (nombres.length <= 1) return nombres.join("");
  return `${nombres.slice(0, -1).join(", ")} y ${nombres[nombres.length - 1]}`;
}

/**
 * La pestaña «Modelo» de Sistema (asistente-modelo-en-caliente, tarea 4): qué
 * modelo responde, elegido entre un servidor local y una API en la nube, con la
 * clave de cada empresa. Un cambio se prueba contra el proveedor antes de poder
 * aplicarse, y rige desde el próximo turno sin reiniciar nada.
 *
 * LA PRUEBA VALE PARA UN PEDIDO, NO PARA LA PANTALLA. `prueba` guarda la firma
 * del pedido que se probó; «Aplicar cambio» sólo se habilita si coincide con la
 * del pedido actual, así que cambiar de modelo, de servidor o la clave escrita
 * lo deshabilita solo, sin que nadie tenga que acordarse de reiniciarlo. El
 * servidor igual re-prueba al aplicar: esto es una guía, no la garantía.
 *
 * SÓLO LLEGA ACÁ QUIEN TIENE `asistente.administrar` (el gate es la pestaña de
 * `SistemaPage`); el backend lo vuelve a exigir en cada endpoint.
 */
export function PanelModeloDelAsistente({ cambios }: PanelModeloDelAsistenteProps) {
  const queryClient = useQueryClient();
  const estado = useQuery({
    queryKey: CLAVE_ESTADO_DEL_MODELO,
    queryFn: obtenerEstadoDelModelo,
  });

  // `null` hasta que el administrador toca algo: mientras tanto la selección es
  // la derivada de lo que está en uso, y se recalcula si eso cambia.
  const [eleccion, setEleccion] = useState<SeleccionDelModelo | null>(null);
  const [claveEscrita, setClaveEscrita] = useState({ empresa: "", texto: "" });
  const [prueba, setPrueba] = useState<PruebaGuardada | null>(null);
  const [confirmando, setConfirmando] = useState(false);
  const [aplicando, setAplicando] = useState(false);
  const [reemplazo, setReemplazo] = useState<(ReemplazoEnCurso & { empresa: string }) | null>(null);
  const [anuncio, setAnuncio] = useState("");

  const contadorDePruebas = useRef(0);
  const botonProbarRef = useRef<HTMLButtonElement>(null);
  const botonAplicarRef = useRef<HTMLButtonElement>(null);
  const botonReemplazarRef = useRef<HTMLButtonElement>(null);
  const focoPendiente = useRef<"probar" | "aplicar" | "reemplazar" | null>(null);

  // El foco vuelve al botón correspondiente una vez que el render que lo
  // vuelve a montar ya ocurrió (confirmación cerrada, panel de reemplazo cerrado).
  useEffect(() => {
    const destino =
      focoPendiente.current === "probar"
        ? botonProbarRef.current
        : focoPendiente.current === "aplicar"
          ? botonAplicarRef.current
          : focoPendiente.current === "reemplazar"
            ? botonReemplazarRef.current
            : null;
    if (destino) {
      destino.focus();
      focoPendiente.current = null;
    }
  });

  const datos = estado.data;

  if (estado.isPending) {
    return <p role="status">Cargando el modelo del asistente…</p>;
  }

  if (estado.isError || !datos) {
    return (
      <div className="adoc-asistente-administracion">
        <InlineAlert severity="danger" title="No se pudo cargar el modelo del asistente.">
          <Button variant="secondary" size="sm" onClick={() => void estado.refetch()}>
            Reintentar
          </Button>
        </InlineAlert>
      </div>
    );
  }

  const seleccion = eleccion ?? seleccionInicial(datos);
  const nube = gruposEnLaNube(datos);
  const grupos: GrupoDeModelosEnLaNube[] = nube.map((g) => ({
    empresa: g.empresa,
    nombre: nombreDeLaEmpresa(g.empresa),
    credencial: g.credencial,
    modelos: g.modelos,
  }));
  const grupoElegido = grupos.find((g) => g.empresa === seleccion.empresa);
  const esNube = seleccion.modo === "nube";
  const claveGuardada = grupoElegido?.credencial?.estado === "guardada";
  const textoDeClave =
    esNube && !claveGuardada && claveEscrita.empresa === seleccion.empresa
      ? claveEscrita.texto.trim()
      : "";
  const claveNueva = textoDeClave === "" ? undefined : textoDeClave;
  const faltaLaClave = esNube && !claveGuardada && !claveNueva;

  const destino = destinoDe(seleccion);
  const pedido = pedidoDe(destino, claveNueva);
  const firma = firmaDelPedido(pedido);
  const alcance = esNube ? "nube" : "local";
  const vista: VistaDePrueba =
    prueba && prueba.firma === firma ? prueba.vista : { estado: "inactiva" };

  const nombreEnUso = nombreDelModelo(datos.enUso.empresa, datos.enUso.modelo);
  const nombreDelDestino = destino ? nombreDelModelo(destino.empresa, destino.modelo) : "";
  const puedeProbar =
    pedido !== null &&
    !faltaLaClave &&
    !(esNube && !claveGuardada && !datos.cifradoDisponible) &&
    vista.estado !== "probando" &&
    !aplicando;
  const puedeAplicar = vista.estado === "ok" && !aplicando;

  const enUsoEsLocal = esServidorLocal(datos, datos.enUso.empresa);
  const enUsoEsNube = esEmpresaEnLaNube(datos, datos.enUso.empresa);
  const conClave = nube.filter((g) => g.credencial?.estado === "guardada").length;

  function cambiarSeleccion(cambio: Partial<SeleccionDelModelo>) {
    setEleccion({ ...seleccion, ...cambio });
    // Un reemplazo a medias es de una empresa: elegir otra cosa lo descarta.
    if (cambio.empresa !== undefined && cambio.empresa !== seleccion.empresa) setReemplazo(null);
    if (cambio.modo !== undefined && cambio.modo !== seleccion.modo) setReemplazo(null);
  }

  const elegirServidor = (servidor: string) =>
    cambiarSeleccion({ servidor, modeloLocal: modelosDelServidor(datos, servidor)[0] ?? null });

  async function probar() {
    if (!pedido || !firma) return;
    const numero = ++contadorDePruebas.current;
    setPrueba({ firma, vista: { estado: "probando" } });
    let resultado: VistaDePrueba;
    try {
      const respuesta = await probarModelo(pedido);
      resultado = respuesta.exito
        ? { estado: "ok", latenciaMs: respuesta.latenciaMs ?? 0 }
        : vistaDeFallo(respuesta.codigo ?? "desconocido", alcance);
    } catch (error) {
      resultado = vistaDeFallo(codigoDeError(error), alcance);
    }
    // Una prueba de un pedido que ya no es el actual no pisa a la más nueva.
    if (numero === contadorDePruebas.current) setPrueba({ firma, vista: resultado });
  }

  async function aplicar() {
    if (!pedido || !firma) return;
    setAplicando(true);
    try {
      const aplicado = await aplicarModelo(pedido);
      await queryClient.invalidateQueries({ queryKey: CLAVE_MODELO });
      setClaveEscrita({ empresa: "", texto: "" });
      setPrueba(null);
      setConfirmando(false);
      focoPendiente.current = "probar";
      setAnuncio(
        `Modelo cambiado a ${nombreDelModelo(aplicado.enUso.empresa, aplicado.enUso.modelo)}. Rige desde el próximo turno.`,
      );
    } catch (error) {
      // El servidor re-prueba y puede rechazar lo que la pantalla daba por
      // probado (422): no se cambió nada, y se dice por qué.
      setPrueba({ firma, vista: vistaDeFallo(codigoDeError(error), alcance) });
      setConfirmando(false);
      focoPendiente.current = "probar";
    } finally {
      setAplicando(false);
    }
  }

  async function volverAlServidor() {
    await restablecerModelo();
    await queryClient.invalidateQueries({ queryKey: CLAVE_MODELO });
    setEleccion(null);
    setClaveEscrita({ empresa: "", texto: "" });
    setPrueba(null);
    setConfirmando(false);
    setReemplazo(null);
    setAnuncio("Se volvió a la configuración del servidor.");
  }

  async function confirmarReemplazo(clave: string) {
    if (!grupoElegido) return;
    setReemplazo({ empresa: grupoElegido.empresa, validando: true, error: null });
    try {
      const reemplazada = await reemplazarClaveDeEmpresa(grupoElegido.empresa, clave);
      await queryClient.invalidateQueries({ queryKey: CLAVE_MODELO });
      setReemplazo(null);
      focoPendiente.current = "reemplazar";
      setAnuncio(
        `Clave de ${grupoElegido.nombre} reemplazada. Ahora termina en ${reemplazada.pista}.`,
      );
    } catch (error) {
      // La clave vieja sigue guardada y en uso: el panel queda abierto.
      setReemplazo({
        empresa: grupoElegido.empresa,
        validando: false,
        error: textoDelCodigo(codigoDeError(error), "nube"),
      });
    }
  }

  // Guardar un precio escribe una versión nueva; si el servidor la rechaza, el
  // error sube hasta el formulario, que lo muestra y queda abierto.
  async function guardarPrecio(pedidoDePrecio: PedidoDePrecio) {
    await cargarPrecio(pedidoDePrecio);
    await queryClient.invalidateQueries({ queryKey: CLAVE_MODELO });
    setAnuncio(
      `Precio de ${nombreDelModelo(pedidoDePrecio.empresa, pedidoDePrecio.modelo)} guardado.`,
    );
  }

  const reemplazoVisible =
    reemplazo && reemplazo.empresa === seleccion.empresa && esNube && claveGuardada
      ? { validando: reemplazo.validando, error: reemplazo.error }
      : null;

  return (
    <div className="adoc-asistente-administracion">
      {/* Única región viva de la pestaña para los resultados que no dejan un
          mensaje visible (cambio aplicado, vuelta al servidor, clave
          reemplazada): el foco no se mueve y el cambio igual se anuncia. */}
      <p role="status" aria-live="polite" className="adoc-asistente-admin-anuncio">
        {anuncio}
      </p>

      <div className="adoc-asistente-admin-encabezado">
        <div>
          <h2 className="adoc-asistente-admin-titulo">Modelo del asistente</h2>
          <p className="adoc-asistente-admin-copete">
            Elegí si responde el modelo local o una API en la nube. Cada empresa guarda su propia
            clave, y el cambio rige desde el próximo turno sin reiniciar el servidor.
          </p>
        </div>
      </div>

      <BannerDelModeloEnUso
        enUso={datos.enUso}
        enmascara={datos.enUso.origen === "pantalla" && enUsoEsNube}
        onVolverAlServidor={volverAlServidor}
      />

      <TarjetasDeModalidad
        modalidad={seleccion.modo}
        onElegir={(modo) => cambiarSeleccion({ modo })}
        tarjetas={[
          {
            id: "local",
            nombre: "Modelo local",
            descripcion: "Un servidor de inferencia propio. Los datos no salen de la universidad.",
            nota:
              datos.servidoresLocales.length === 0
                ? "No hay servidores locales configurados."
                : "Sin costo por token.",
            enUso: enUsoEsLocal,
            deshabilitada: datos.servidoresLocales.length === 0,
          },
          {
            id: "nube",
            nombre: "API en la nube",
            descripcion: `Modelos de ${unirNombres(grupos.map((g) => g.nombre))}. Cada empresa necesita su clave de API.`,
            nota: `${conClave} de ${grupos.length} empresas con clave guardada. Cada turno tiene costo.`,
            enUso: enUsoEsNube,
            deshabilitada: grupos.length === 0,
          },
        ]}
      />

      <section
        className="sistema-tarjeta adoc-asistente-modelo-configuracion"
        aria-labelledby="modelo-titulo-configuracion"
      >
        <span id="modelo-titulo-configuracion" className="sistema-tarjeta-nombre">
          {esNube ? "Modelo en la nube" : "Modelo local"}
        </span>

        {esNube ? (
          <PanelDeModeloEnLaNube
            grupos={grupos}
            seleccion={
              seleccion.empresa && seleccion.modeloNube
                ? { empresa: seleccion.empresa, modelo: seleccion.modeloNube }
                : null
            }
            enUso={
              enUsoEsNube ? { empresa: datos.enUso.empresa, modelo: datos.enUso.modelo } : null
            }
            onElegir={(empresa, modelo) => cambiarSeleccion({ empresa, modeloNube: modelo })}
            cifradoDisponible={datos.cifradoDisponible}
            claveNueva={claveEscrita.empresa === seleccion.empresa ? claveEscrita.texto : ""}
            onClaveNueva={(texto) => setClaveEscrita({ empresa: seleccion.empresa ?? "", texto })}
            reemplazo={reemplazoVisible}
            onReemplazar={() =>
              setReemplazo({ empresa: seleccion.empresa ?? "", validando: false, error: null })
            }
            onConfirmarReemplazo={(clave) => void confirmarReemplazo(clave)}
            onCancelarReemplazo={() => {
              setReemplazo(null);
              focoPendiente.current = "reemplazar";
            }}
            botonReemplazarRef={botonReemplazarRef}
          />
        ) : (
          <PanelDeModeloLocal
            servidores={datos.servidoresLocales}
            servidor={seleccion.servidor}
            modelos={modelosDelServidor(datos, seleccion.servidor)}
            modelo={seleccion.modeloLocal}
            onServidor={elegirServidor}
            onModelo={(modeloLocal) => cambiarSeleccion({ modeloLocal })}
          />
        )}

        {destino && (
          <PrecioDelModelo
            key={nombreDelDestino}
            empresa={destino.empresa}
            modelo={destino.modelo}
            nombre={nombreDelDestino}
            vigente={precioDelModelo(datos, destino.empresa, destino.modelo)}
            onGuardar={guardarPrecio}
          />
        )}

        <AccionesDelModelo
          prueba={vista}
          destino={nombreDelDestino}
          vigente={nombreEnUso}
          claveAGuardarDe={claveNueva && grupoElegido ? grupoElegido.nombre : null}
          puedeProbar={puedeProbar}
          puedeAplicar={puedeAplicar}
          confirmando={confirmando && vista.estado === "ok"}
          aplicando={aplicando}
          onProbar={() => void probar()}
          onAplicar={() => setConfirmando(true)}
          onCancelar={() => {
            setConfirmando(false);
            focoPendiente.current = "aplicar";
          }}
          onConfirmar={() => void aplicar()}
          botonProbarRef={botonProbarRef}
          botonAplicarRef={botonAplicarRef}
        />
      </section>

      {cambios}
    </div>
  );
}
