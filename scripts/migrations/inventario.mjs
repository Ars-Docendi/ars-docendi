import { contextos, leer, listar, rutaSegura, verificarRuta } from "./comun.mjs";
import { existsSync, lstatSync } from "node:fs";

// Subconjunto deliberado de C#: atributos con strings literales y typeof simple.
// No interpreta DDL, no evalúa C# y no sustituye la verificación del assembly compilado.
function tokens(texto) {
  const resultado = [];
  let i = 0;
  while (i < texto.length) {
    if (/\s/.test(texto[i])) {
      i++;
      continue;
    }
    if (texto.startsWith("//", i)) {
      i = texto.indexOf("\n", i);
      if (i < 0) break;
      continue;
    }
    if (texto.startsWith("/*", i)) {
      const fin = texto.indexOf("*/", i + 2);
      if (fin < 0) throw new Error("Comentario C# incompleto");
      i = fin + 2;
      continue;
    }
    if (texto[i] === '"') {
      let valor = "";
      i++;
      while (i < texto.length && texto[i] !== '"') {
        if (texto[i] === "\\") {
          valor += texto[i++];
          if (i === texto.length) throw new Error("String C# incompleto");
        }
        valor += texto[i++];
      }
      if (texto[i++] !== '"') throw new Error("String C# incompleto");
      resultado.push({ tipo: "string", valor });
      continue;
    }
    if (texto[i] === "'") {
      i++;
      while (i < texto.length && texto[i] !== "'") {
        if (texto[i] === "\\") i++;
        i++;
      }
      if (texto[i++] !== "'") throw new Error("Char C# incompleto");
      resultado.push({ tipo: "char", valor: "" });
      continue;
    }
    const identificador = /^[A-Za-z_][A-Za-z0-9_]*/.exec(texto.slice(i));
    if (identificador) {
      resultado.push({ tipo: "codigo", valor: identificador[0] });
      i += identificador[0].length;
    } else resultado.push({ tipo: "codigo", valor: texto[i++] });
  }
  return resultado;
}
function atributo(lista, nombre) {
  const encontrados = [];
  for (let i = 0; i < lista.length; i++) {
    if (lista[i].valor !== "[" || lista[i + 1]?.valor !== nombre || lista[i + 2]?.valor !== "(")
      continue;
    let j = i + 3;
    while (j < lista.length && lista[j].valor !== "]") j++;
    if (lista[j - 1]?.valor !== ")") throw new Error(`Atributo inválido: ${nombre}`);
    encontrados.push(lista.slice(i + 3, j - 1));
  }
  if (encontrados.length !== 1) throw new Error(`Se requiere exactamente un atributo ${nombre}`);
  return encontrados[0];
}
function literales(lista, nombre) {
  if (
    !lista.length ||
    lista.length % 2 !== 1 ||
    lista.some((token, i) => (i % 2 === 0 ? token.tipo !== "string" : token.valor !== ","))
  )
    throw new Error(`${nombre}: sólo se admiten strings literales ordenados`);
  return lista.filter((_, i) => i % 2 === 0).map((t) => t.valor);
}
export function inventariar(raiz, clasificados = {}) {
  const migraciones = [];
  const archivos = [];
  const ids = new Set();
  const recursosUsados = new Set();
  for (const [contexto, config] of Object.entries(contextos)) {
    const wrappers = listar(raiz, config.directorio).filter((ruta) => ruta.endsWith(".cs"));
    // ModelSnapshot es el estado actual mutable, no una operación histórica.
    archivos.push(...wrappers.filter((ruta) => !ruta.endsWith("ModelSnapshot.cs")));
    for (const ruta of wrappers) {
      if (ruta.endsWith("ModelSnapshot.cs") || ruta.endsWith(".Designer.cs")) continue;
      try {
        const lista = tokens(leer(raiz, ruta));
        const codigo = lista.map((t) => (t.tipo === "codigo" ? t.valor : "#")).join(" ");
        const clases = [...codigo.matchAll(/\bclass (\w+) : Migration\b/g)];
        if (clases.length !== 1) throw new Error("Se requiere una clase : Migration por wrapper");
        const clase = clases[0][1];
        const [id, ...sobrantes] = literales(atributo(lista, "Migration"), "Migration");
        if (sobrantes.length || !/^\d{14}_[A-Za-z][A-Za-z0-9_]*$/.test(id))
          throw new Error(`ID inválido: ${id}`);
        const marca = id.slice(0, 14);
        const fecha = `${marca.slice(0, 4)}-${marca.slice(4, 6)}-${marca.slice(6, 8)}T${marca.slice(8, 10)}:${marca.slice(10, 12)}:${marca.slice(12, 14)}Z`;
        if (
          Number.isNaN(Date.parse(fecha)) ||
          new Date(fecha).toISOString().replace(/\D/g, "").slice(0, 14) !== marca
        )
          throw new Error(`Timestamp inválido: ${id}`);
        if (ids.has(id)) throw new Error(`ID duplicado: ${id}`);
        ids.add(id);
        const db = atributo(lista, "DbContext")
          .map((t) => t.valor)
          .join(" ");
        if (db !== `typeof ( ${config.db} )`)
          throw new Error(`DbContext incorrecto para ${contexto}: ${db}`);
        const recursos = literales(atributo(lista, "RecursosMigracionSql"), "RecursosMigracionSql");
        for (const recurso of recursos) {
          if (
            !rutaSegura(recurso) ||
            !/^[a-z]+\/[A-Za-z0-9_-]+\.sql$/.test(recurso) ||
            !config.schemas.includes(recurso.split("/")[0])
          )
            throw new Error(`Recurso/ruta inválida para ${contexto}: ${recurso}`);
          if (recursosUsados.has(recurso)) throw new Error(`Recurso duplicado: ${recurso}`);
          recursosUsados.add(recurso);
          const absoluto = verificarRuta(raiz, `database/${recurso}`);
          if (!existsSync(absoluto)) throw new Error(`Recurso faltante: ${recurso}`);
          if (!lstatSync(absoluto).isFile())
            throw new Error(`Recurso no es archivo SQL: ${recurso}`);
        }
        const llamada = `migrationBuilder . AplicarRecursosSql ( typeof ( ${clase} ) )`;
        const up = codigo.split(/\bUp \(/)[1]?.split(/\bDown \(/)[0];
        if (!up?.includes(llamada))
          throw new Error(
            `Up debe consumir metadata mediante AplicarRecursosSql(typeof(${clase}))`,
          );
        migraciones.push({ contexto, id, clase, ruta, recursos });
      } catch (error) {
        throw new Error(`${ruta}: ${error.message}`);
      }
    }
  }
  const sql = listar(raiz, "database").filter((ruta) => ruta.endsWith(".sql"));
  for (const ruta of sql) {
    const recurso = ruta.slice("database/".length);
    if (!recursosUsados.has(recurso) && !Object.hasOwn(clasificados, recurso))
      throw new Error(`SQL sin consumidor ni clasificación explícita: ${recurso}`);
  }
  for (const [recurso, motivo] of Object.entries(clasificados)) {
    if (
      !rutaSegura(recurso) ||
      !recurso.endsWith(".sql") ||
      typeof motivo !== "string" ||
      motivo.trim().length < 10
    )
      throw new Error(`Clasificación inválida: ${recurso}`);
    if (!sql.includes(`database/${recurso}`))
      throw new Error(`Clasificación de SQL inexistente: ${recurso}`);
    if (recursosUsados.has(recurso))
      throw new Error(`SQL consumido y también clasificado: ${recurso}`);
  }
  archivos.push(...sql);
  return { migraciones, archivos: [...new Set(archivos)].sort() };
}
