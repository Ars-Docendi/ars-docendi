import { readdir, readFile } from "node:fs/promises";
import { extname, relative } from "node:path";

const raiz = new URL("../src/", import.meta.url);
const prohibidos = [
  /from\s+["'][^"']*\/mock\//,
  /pedidos(?:Seed|Store)/,
  /adoc\.mock\.pedidos/,
  /adoc\.dev\.mock(?:User|Rol)/,
];
const errores = [];

async function recorrer(url) {
  for (const entrada of await readdir(url, { withFileTypes: true })) {
    const ruta = new URL(entrada.name, url);
    if (entrada.isDirectory()) {
      await recorrer(new URL(`${entrada.name}/`, url));
      continue;
    }
    if (![".ts", ".tsx"].includes(extname(entrada.name)) || entrada.name.includes(".test."))
      continue;
    const contenido = await readFile(ruta, "utf8");
    if (prohibidos.some((patron) => patron.test(contenido))) {
      errores.push(relative(new URL("..", raiz).pathname, ruta.pathname));
    }
  }
}

await recorrer(raiz);
if (errores.length) {
  throw new Error(`El runtime todavía referencia datos mock:\n${errores.join("\n")}`);
}
