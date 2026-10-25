import axios from "axios";

import type {
  CredencialDeEmpresa,
  EmpresaDelCatalogo,
  EstadoDelModelo,
  ModeloEnUso,
  PedidoDeModelo,
  PedidoDePrecio,
  PrecioDelModelo,
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
    case "precio-obligatorio":
      return "Completá entrada, salida y la fecha de vigencia.";
    case "precio-negativo":
      return "Los montos no pueden ser negativos.";
    case "precio-fuera-de-rango":
      return "Un monto supera el máximo admitido: US$ 10.000 por millón de tokens.";
    case "precio-con-demasiados-decimales":
      return "Un monto tiene más de seis decimales.";
    case "vigencia-anterior-a-la-version-actual":
      return "La fecha de vigencia no puede ser anterior a la de la versión vigente.";
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

// ---------------------------------------------------------------- precios

/** El máximo y los decimales que acepta el backend, en US$ por millón de tokens. */
const PRECIO_MAXIMO = 10_000;
const PRECIO_DECIMALES = 6;

/** El precio vigente de un modelo, o `undefined` si está «sin precio cargado». */
export function precioDelModelo(
  estado: EstadoDelModelo,
  empresa: string,
  modelo: string,
): PrecioDelModelo | undefined {
  return estado.precios.find((p) => p.empresa === empresa && p.modelo === modelo);
}

/** «AAAA-MM-DD» → «DD/MM/AAAA». Un texto que no tiene esa forma se devuelve tal cual. */
export function fechaLegible(iso: string): string {
  const [anio, mes, dia] = iso.split("-");
  return anio && mes && dia ? `${dia}/${mes}/${anio}` : iso;
}

/** US$ por millón de tokens, de dos a seis decimales: «3,00», «0,60», «0,000125». */
export function montoLegible(valor: number): string {
  return valor.toLocaleString("es-AR", {
    minimumFractionDigits: 2,
    maximumFractionDigits: PRECIO_DECIMALES,
    useGrouping: false,
  });
}

/** Hoy, «AAAA-MM-DD», en la zona horaria del navegador. */
export function hoyEnIso(ahora: Date = new Date()): string {
  const mes = String(ahora.getMonth() + 1).padStart(2, "0");
  const dia = String(ahora.getDate()).padStart(2, "0");
  return `${ahora.getFullYear()}-${mes}-${dia}`;
}

export interface FormularioDePrecio {
  entrada: string;
  salida: string;
  cache: string;
  desde: string;
}

export type ErroresDelPrecio = Partial<Record<keyof FormularioDePrecio, string>>;

function errorDelMonto(texto: string, obligatorio: boolean): string | undefined {
  const limpio = texto.trim();
  if (limpio === "") return obligatorio ? "Completá este monto." : undefined;
  const valor = Number(limpio);
  if (!Number.isFinite(valor)) return "Ingresá un número.";
  if (valor < 0) return "No puede ser negativo.";
  if (valor > PRECIO_MAXIMO) return "Supera el máximo: US$ 10.000 por millón.";
  const decimales = limpio.split(".")[1]?.replace(/0+$/, "") ?? "";
  if (decimales.length > PRECIO_DECIMALES) return "Admite hasta seis decimales.";
  return undefined;
}

/** Los errores de campo de un formulario de precio; vacío si se puede enviar. */
export function validarPrecio(formulario: FormularioDePrecio): ErroresDelPrecio {
  const errores: ErroresDelPrecio = {};
  const entrada = errorDelMonto(formulario.entrada, true);
  const salida = errorDelMonto(formulario.salida, true);
  const cache = errorDelMonto(formulario.cache, false);
  if (entrada) errores.entrada = entrada;
  if (salida) errores.salida = salida;
  if (cache) errores.cache = cache;
  if (formulario.desde.trim() === "") errores.desde = "Completá la fecha de vigencia.";
  return errores;
}

/** El pedido de un formulario ya validado. Un caché vacío no se envía. */
export function pedidoDePrecio(
  destino: { empresa: string; modelo: string },
  formulario: FormularioDePrecio,
): PedidoDePrecio {
  const pedido: PedidoDePrecio = {
    ...destino,
    entradaPorMillon: Number(formulario.entrada.trim()),
    salidaPorMillon: Number(formulario.salida.trim()),
    vigenteDesde: formulario.desde,
  };
  if (formulario.cache.trim() !== "") pedido.cachePorMillon = Number(formulario.cache.trim());
  return pedido;
}
