import { createHash } from "node:crypto";
import { readFileSync, existsSync } from "node:fs";
import { leer, manifiestoRuta, rutaSegura, verificarRuta } from "./comun.mjs";

export function hash(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}
export function validarManifiesto(valor) {
  if (
    !valor ||
    valor.version !== 1 ||
    !valor.archivos ||
    typeof valor.archivos !== "object" ||
    Array.isArray(valor.archivos) ||
    !valor.clasificados ||
    typeof valor.clasificados !== "object" ||
    Array.isArray(valor.clasificados)
  )
    throw new Error("Manifiesto inválido: se requiere version 1, archivos y clasificados");
  if (!Object.keys(valor.archivos).length) throw new Error("Inventario protegido vacío");
  for (const [ruta, suma] of Object.entries(valor.archivos)) {
    if (
      !rutaSegura(ruta) ||
      !(
        (ruta.startsWith("database/") && ruta.endsWith(".sql")) ||
        (ruta.startsWith("backend/src/") && ruta.includes("/Migrations/") && ruta.endsWith(".cs"))
      ) ||
      !/^[a-f0-9]{64}$/.test(suma)
    )
      throw new Error(`Entrada protegida inválida: ${ruta}`);
  }
  return valor;
}
export function leerManifiesto(raiz) {
  return validarManifiesto(JSON.parse(leer(raiz, manifiestoRuta)));
}
export function comprobarArchivos(raiz, archivos) {
  for (const [ruta, esperado] of Object.entries(archivos)) {
    const absoluto = verificarRuta(raiz, ruta);
    if (!existsSync(absoluto)) throw new Error(`Archivo protegido eliminado: ${ruta}`);
    if (hash(readFileSync(absoluto)) !== esperado)
      throw new Error(`Archivo protegido modificado: ${ruta}`);
  }
}
export function hashes(raiz, rutas) {
  return Object.fromEntries(
    rutas.map((ruta) => [ruta, hash(readFileSync(verificarRuta(raiz, ruta)))]),
  );
}
