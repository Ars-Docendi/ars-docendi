import axios from "axios";

import type {
  CredencialDeEmpresa,
  EmpresaDelCatalogo,
  EstadoDelModelo,
  ModeloEnUso,
  PedidoDeModelo,
} from "../types";

// Lógica pura de la pestaña «Modelo» (asistente-modelo-en-caliente, tarea 4):
// nombres para mostrar, la selección inicial, el pedido que se arma de ella y la
// traducción de los códigos de error del backend a texto. Sin React ni red.

const NOMBRES_DE_SERVIDOR: Record<string, string> = {
  ollama: "Servicio de inferencia compartido",
  local: "Servidor propio",
};

const NOMBRES_DE_EMPRESA: Record<string, string> = {
  anthropic: "Anthropic",
  openai: "OpenAI",
  google: "Google Gemini",
  mistral: "Mistral",
};

/** «clave ••••1234» o «falta la clave»: el estado de la clave de una empresa. */
export function textoDeLaClave(credencial: CredencialDeEmpresa | null): string {
  return credencial?.estado === "guardada" && credencial.pista
    ? `clave ••••${credencial.pista}`
    : "falta la clave";
}

/** El nombre de un servidor local; un identificador que no se conoce se muestra tal cual. */
export function nombreDelServidor(id: string): string {
  return NOMBRES_DE_SERVIDOR[id] ?? id;
}

export function nombreDeLaEmpresa(id: string): string {
  return NOMBRES_DE_EMPRESA[id] ?? id;
}

/** «empresa/modelo», la forma en que el backend nombra cada adaptador. */
export function nombreDelModelo(empresa: string, modelo: string): string {
  return modelo ? `${empresa}/${modelo}` : empresa;
}

export function esServidorLocal(estado: EstadoDelModelo, empresa: string): boolean {
  return (
    estado.servidoresLocales.includes(empresa) ||
    estado.catalogo.some((g) => g.empresa === empresa && g.tipo === "local")
  );
}

export function esEmpresaEnLaNube(estado: EstadoDelModelo, empresa: string): boolean {
  return estado.catalogo.some((g) => g.empresa === empresa && g.tipo === "nube");
}

export function gruposEnLaNube(estado: EstadoDelModelo): EmpresaDelCatalogo[] {
  return estado.catalogo.filter((g) => g.tipo === "nube");
}

export function modelosDelServidor(estado: EstadoDelModelo, servidor: string | null): string[] {
  return estado.catalogo.find((g) => g.empresa === servidor && g.tipo === "local")?.modelos ?? [];
}

export interface SeleccionDelModelo {
  modo: "local" | "nube";
  servidor: string | null;
  modeloLocal: string | null;
  empresa: string | null;
  modeloNube: string | null;
}

/**
 * Lo que la pantalla muestra elegido antes de que el administrador toque nada:
 * el modelo en uso si el catálogo lo conoce, y si no (p. ej. el proveedor
 * `simulado` del ambiente) el primer servidor y la primera empresa disponibles.
 */
export function seleccionInicial(estado: EstadoDelModelo): SeleccionDelModelo {
  const { enUso } = estado;
  const nube = gruposEnLaNube(estado);
  const enUsoEsLocal = esServidorLocal(estado, enUso.empresa);
  const enUsoEsNube = esEmpresaEnLaNube(estado, enUso.empresa);

  const servidor = enUsoEsLocal ? enUso.empresa : (estado.servidoresLocales[0] ?? null);
  const modelosLocales = modelosDelServidor(estado, servidor);
  const modeloLocal =
    enUsoEsLocal && modelosLocales.includes(enUso.modelo)
      ? enUso.modelo
      : (modelosLocales[0] ?? null);

  const empresa = enUsoEsNube ? enUso.empresa : (nube[0]?.empresa ?? null);
  const modelosNube = nube.find((g) => g.empresa === empresa)?.modelos ?? [];
  const modeloNube =
    enUsoEsNube && modelosNube.includes(enUso.modelo) ? enUso.modelo : (modelosNube[0] ?? null);

  const modo = enUsoEsNube || estado.servidoresLocales.length === 0 ? "nube" : "local";
  return { modo, servidor, modeloLocal, empresa, modeloNube };
}

/** El destino de la selección, o `null` si le falta algo para poder probarse. */
export function destinoDe(
  seleccion: SeleccionDelModelo,
): { empresa: string; modelo: string } | null {
  if (seleccion.modo === "local") {
    return seleccion.servidor && seleccion.modeloLocal
      ? { empresa: seleccion.servidor, modelo: seleccion.modeloLocal }
      : null;
  }
  return seleccion.empresa && seleccion.modeloNube
    ? { empresa: seleccion.empresa, modelo: seleccion.modeloNube }
    : null;
}

export function pedidoDe(
  destino: { empresa: string; modelo: string } | null,
  claveNueva: string | undefined,
): PedidoDeModelo | null {
  if (!destino) return null;
  return claveNueva ? { ...destino, claveNueva } : { ...destino };
}

/**
 * Identifica «lo que se probó»: una prueba vale sólo para el pedido que la
 * originó. Cambiar de modelo, de servidor o escribir otra clave cambia la firma
 * y la prueba anterior deja de habilitar «Aplicar cambio».
 */
export function firmaDelPedido(pedido: PedidoDeModelo | null): string | null {
  return pedido ? `${pedido.empresa}|${pedido.modelo}|${pedido.claveNueva ?? ""}` : null;
}

export function esElModeloEnUso(enUso: ModeloEnUso, empresa: string, modelo: string): boolean {
  return enUso.empresa === empresa && enUso.modelo === modelo;
}

/** El código de un fallo: el `codigo` del ProblemDetails, o uno propio para 403 y red. */
export function codigoDeError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 403) return "sin-permiso";
    const codigo = (error.response?.data as { codigo?: unknown } | undefined)?.codigo;
    if (typeof codigo === "string") return codigo;
    if (error.response === undefined) return "sin-conexion";
  }
  return "desconocido";
}

/** `saturado` no es un fallo de la configuración: se muestra aparte, como aviso. */
export const CODIGO_SATURADO = "saturado";

/** El texto de un código de la prueba o de un rechazo, para un servidor local o una API en la nube. */
export function textoDelCodigo(codigo: string, alcance: "local" | "nube"): string {
  const local = alcance === "local";
  switch (codigo) {
    case "clave-invalida":
      return local
        ? "El servidor rechazó su credencial. Se corrige en la configuración del servidor."
        : "El proveedor rechazó la clave.";
    case "sin-credito":
      return "La cuenta del proveedor no tiene crédito.";
    case "modelo-inexistente":
      return local ? "El servidor no tiene ese modelo." : "El proveedor no reconoce ese modelo.";
    case "inalcanzable":
      return local
        ? "No se pudo conectar con el servidor."
        : "No se pudo conectar con el proveedor.";
    case CODIGO_SATURADO:
      return "El servicio está ocupado, probá en unos segundos.";
    case "clave-maestra-no-disponible":
      return "Guardar claves no está disponible: el servidor no tiene una clave maestra válida. Los modelos locales siguen disponibles.";
    case "clave-demasiado-corta":
      return "La clave es demasiado corta.";
    case "empresa-sin-clave":
      return "Falta la clave de esta empresa.";
    case "empresa-desconocida":
    case "modelo-fuera-del-catalogo":
    case "servidor-local-no-configurado":
    case "credencial-local-no-admitida":
      return "Esa combinación de empresa y modelo no está disponible en este servidor.";
    case "sin-permiso":
      return "No tenés permiso para administrar el modelo.";
    case "sin-conexion":
      return "No se pudo comunicar con el servidor. Revisá tu conexión.";
    default:
      return "No se pudo completar la operación. Probá de nuevo.";
  }
}
