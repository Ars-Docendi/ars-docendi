import { apiClient } from "./client";

export type PropositoArchivo =
  "cv" | "dni_frente" | "dni_dorso" | "justificativo" | "documento_proyecto";

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
  const { data: sesion } = await apiClient.post<SesionCarga>("/api/archivos/cargas", {
    proposito,
    nombreOriginal: archivo.name,
    mimeDeclarado: archivo.type,
    tamanoBytes: archivo.size,
  });
  await apiClient.put(sesion.urlSubida, archivo, {
    headers: { "Content-Type": archivo.type },
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
