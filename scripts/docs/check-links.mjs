// Falla si un .md versionado enlaza a un archivo relativo inexistente.
// Los changes archivados son históricos y no se revisan.
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";

const archivos = execFileSync("git", ["ls-files", "*.md"], { encoding: "utf8" })
  .split("\n")
  .filter((f) => f && !f.startsWith("openspec/changes/archive/"));

const rotos = [];
for (const archivo of archivos) {
  const texto = readFileSync(archivo, "utf8");
  const definiciones = new Map(
    [...texto.matchAll(/^\s*\[([^\]]+)\]:\s*<?([^\s>#]+)/gm)].map(([, e, d]) => [
      e.toLowerCase(),
      d,
    ]),
  );
  const destinos = [
    ...[...texto.matchAll(/\]\(<?([^)\s>#]+)>?(?:#[^)\s]*)?(?:\s+(?:"[^"]*"|'[^']*'))?\s*\)/g)].map(
      (m) => m[1],
    ),
    ...[...texto.matchAll(/\]\[([^\]]*)\]/g)]
      .map((m) => definiciones.get(m[1].toLowerCase()))
      .filter(Boolean),
    ...definiciones.values(),
  ];
  for (const destino of new Set(destinos)) {
    if (/^[a-z]+:/i.test(destino)) continue;
    if (!existsSync(resolve(dirname(archivo), decodeURI(destino)))) {
      rotos.push(`${archivo} -> ${destino}`);
    }
  }
}

if (rotos.length > 0) {
  console.error(`Links rotos (${rotos.length}):\n${rotos.join("\n")}`);
  process.exit(1);
}
console.log(`Links relativos OK en ${archivos.length} archivos.`);
