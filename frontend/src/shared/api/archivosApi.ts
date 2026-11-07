import { apiClient } from "./client";

export type PropositoArchivo =
  "cv" | "dni_frente" | "dni_dorso" | "justificativo" | "documento_proyecto";

type Formato = "pdf" | "imagen";

/** Espejo de `PropositosArchivo.EsPdf` y `ReglasArchivos.ValidarInicio` del backend. */
const FORMATO_POR_PROPOSITO: Record<PropositoArchivo, Formato> = {
  cv: "pdf",
  justificativo: "pdf",
  documento_proyecto: "pdf",
  dni_frente: "imagen",
  dni_dorso: "imagen",
};

const MIME_POR_EXTENSION: Record<Formato, Record<string, string>> = {
  pdf: { ".pdf": "application/pdf" },
  imagen: { ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".png": "image/png" },
};

/**
 * Valor de `accept` para el selector de archivos. Lleva el MIME y también las extensiones:
 * el diálogo de Windows filtra por extensión y el de macOS por tipo.
 */
export function acceptDe(proposito: PropositoArchivo): string {
  const porExtension = MIME_POR_EXTENSION[FORMATO_POR_PROPOSITO[proposito]];
  return [...new Set(Object.values(porExtension)), ...Object.keys(porExtension)].join(",");
}

export function mensajeFormatoInvalido(proposito: PropositoArchivo): string {
  return FORMATO_POR_PROPOSITO[proposito] === "pdf"
    ? "Tiene que ser un archivo PDF."
    : "Tiene que ser una imagen JPG o PNG.";
}

/**
 * MIME a declarar, o `null` si el archivo no es de un formato admitido. Se deduce de la
 * extensión (que el backend también exige para los PDF) porque `File.type` lo informa el sistema operativo y no es confiable: en Windows
 * puede llegar vacío o como `application/x-pdf`. El contenido real lo verifica el backend.
 */
export function mimeAdmitido(proposito: PropositoArchivo, archivo: File): string | null {
  const porExtension = MIME_POR_EXTENSION[FORMATO_POR_PROPOSITO[proposito]];
  const nombre = archivo.name.toLowerCase();
  const punto = nombre.lastIndexOf(".");
  return (punto >= 0 && porExtension[nombre.slice(punto)]) || null;
}

interface SesionCarga {
  archivoId: string;
  urlSubida: string;
  expiraEn: string;
}

export interface ArchivoConfirmado {
  id: string;
  proposito: PropositoArchivo;
  nombreOriginal: string;
  mimeDeclarado: string;
  mimeDetectado: string | null;
  tamanoBytes: number;
  sha256: string | null;
  estado: string;
}

export async function subirArchivo(
  proposito: PropositoArchivo,
  archivo: File,
): Promise<ArchivoConfirmado> {
  const mime = mimeAdmitido(proposito, archivo);
  if (!mime) throw new Error(mensajeFormatoInvalido(proposito));
  const { data: sesion } = await apiClient.post<SesionCarga>("/api/archivos/cargas", {
    proposito,
    nombreOriginal: archivo.name,
    mimeDeclarado: mime,
    tamanoBytes: archivo.size,
  });
  await apiClient.put(sesion.urlSubida, archivo, {
    headers: { "Content-Type": mime },
    transformRequest: [(valor) => valor],
  });
  const sha256 = await calcularSha256(archivo);
  const { data } = await apiClient.post<ArchivoConfirmado>(
    `/api/archivos/cargas/${sesion.archivoId}/confirmar`,
    { archivoId: sesion.archivoId, sha256, tamanoBytes: archivo.size },
  );
  if (data.estado !== "disponible") {
    throw new Error(
      `El archivo ${data.nombreOriginal} no quedó disponible: estado ${data.estado}.`,
    );
  }
  return data;
}

async function calcularSha256(archivo: Blob): Promise<string> {
  const buffer =
    typeof archivo.arrayBuffer === "function"
      ? await archivo.arrayBuffer()
      : await new Response(archivo).arrayBuffer();
  const hash = await crypto.subtle.digest("SHA-256", buffer);
  return [...new Uint8Array(hash)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}
