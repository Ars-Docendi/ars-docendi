import { readdirSync, lstatSync, existsSync, readFileSync } from "node:fs";
import { resolve, join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

export const raizPredeterminada = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
export const contextos = {
  Identity: {
    schema: "identity",
    db: "IdentityDbContext",
    espacio: "ArsDocendi.Shared.Identity.Migrations",
    directorio: "backend/src/ArsDocendi.Shared/Identity/Migrations",
    schemas: ["identity", "audit"],
  },
  Storage: {
    schema: "storage",
    db: "AlmacenamientoDbContext",
    espacio: "ArsDocendi.Storage.Infrastructure.Migrations",
    directorio: "backend/src/ArsDocendi.Storage/Infrastructure/Migrations",
    schemas: ["storage"],
  },
  Designaciones: {
    schema: "designaciones",
    db: "DesignacionesDbContext",
    espacio: "Modules.Designaciones.Infrastructure.Migrations",
    directorio: "backend/src/Modules.Designaciones/Infrastructure/Migrations",
    schemas: ["designaciones"],
  },
  Portal: {
    schema: "portal",
    db: "PortalDbContext",
    espacio: "Modules.Portal.Infrastructure.Migrations",
    directorio: "backend/src/Modules.Portal/Infrastructure/Migrations",
    schemas: ["portal"],
  },
  Aulas: {
    schema: "aulas",
    db: "AulasDbContext",
    espacio: "Modules.Aulas.Infrastructure.Migrations",
    directorio: "backend/src/Modules.Aulas/Infrastructure/Migrations",
    schemas: ["aulas"],
  },
  Tareas: {
    schema: "tareas",
    db: "TareasDbContext",
    espacio: "Modules.Tareas.Infrastructure.Migrations",
    directorio: "backend/src/Modules.Tareas/Infrastructure/Migrations",
    schemas: ["tareas"],
  },
};
export const manifiestoRuta = "database/migraciones-protegidas.json";
export function argumentos(args, opcionesConValor = []) {
  const opciones = {};
  const posiciones = [];
  for (let i = 0; i < args.length; i++) {
    const token = args[i];
    if (token.startsWith("--")) {
      if (token in opciones) throw new Error(`Opción repetida: ${token}`);
      if (opcionesConValor.includes(token)) {
        const valor = args[++i];
        if (!valor || valor.startsWith("--")) throw new Error(`Falta valor para ${token}`);
        opciones[token] = valor;
      } else if (token === "--dry-run" || token === "--help") opciones[token] = true;
      else throw new Error(`Opción desconocida: ${token}`);
    } else posiciones.push(token);
  }
  return { opciones, posiciones, raiz: resolve(opciones["--root"] ?? raizPredeterminada) };
}
export function rutaSegura(ruta) {
  return (
    typeof ruta === "string" &&
    /^[A-Za-z0-9_./-]+$/.test(ruta) &&
    !ruta.startsWith("/") &&
    ruta.split("/").every((p) => p && p !== "." && p !== "..")
  );
}
export function verificarRuta(raiz, ruta) {
  if (!rutaSegura(ruta)) throw new Error(`Ruta insegura: ${ruta}`);
  let actual = raiz;
  if (lstatSync(actual).isSymbolicLink()) throw new Error(`Symlink no permitido: ${actual}`);
  for (const segmento of ruta.split("/")) {
    actual = join(actual, segmento);
    if (existsSync(actual) && lstatSync(actual).isSymbolicLink())
      throw new Error(`Symlink no permitido: ${ruta}`);
  }
  return actual;
}
export function listar(raiz, ruta) {
  const absoluto = verificarRuta(raiz, ruta);
  if (!existsSync(absoluto)) return [];
  return readdirSync(absoluto, { withFileTypes: true })
    .sort((a, b) => a.name.localeCompare(b.name))
    .flatMap((entrada) => {
      const siguiente = `${ruta}/${entrada.name}`;
      if (entrada.isSymbolicLink()) throw new Error(`Symlink no permitido: ${siguiente}`);
      return entrada.isDirectory() ? listar(raiz, siguiente) : [siguiente];
    });
}
export function leer(raiz, ruta) {
  return readFileSync(verificarRuta(raiz, ruta), "utf8");
}
export function principal(accion) {
  try {
    accion();
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
