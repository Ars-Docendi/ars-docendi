import { argumentos, contextos, leer, listar, principal, verificarRuta } from "./comun.mjs";
import { mkdirSync, writeFileSync, unlinkSync } from "node:fs";
import { dirname } from "node:path";

principal(() => {
  const { opciones, posiciones, raiz } = argumentos(process.argv.slice(2), ["--root"]);
  if (opciones["--help"]) {
    console.log(
      "node scripts/migrations/scaffold.mjs <Identity|Storage|Designaciones|Portal|Aulas|Tareas> <NombrePascalCase> [--dry-run] [--root directorio]",
    );
    return;
  }
  const [contexto, nombre] = posiciones;
  if (posiciones.length !== 2 || !Object.hasOwn(contextos, contexto))
    throw new Error("Se requiere contexto conocido y nombre. Usar --help.");
  if (!/^[A-Z][A-Za-z0-9]{0,79}$/.test(nombre))
    throw new Error(
      "Nombre inválido: usar PascalCase ASCII, hasta 80 caracteres, sin rutas ni separadores.",
    );
  const config = contextos[contexto];
  const clase = `${nombre}${contexto}`;
  const fecha = new Date().toISOString().replace(/\D/g, "").slice(0, 14);
  const id = `${fecha}_${clase}`;
  const recurso = `${config.schema}/${id}.sql`;
  const archivos = [
    {
      ruta: `database/${recurso}`,
      contenido: `-- ${id} (${contexto}). Completar el DDL antes de aplicar.\n-- No editar después de publicar: crear otra migración incremental.\n`,
    },
    {
      ruta: `${config.directorio}/${id}.cs`,
      contenido: `using ArsDocendi.Shared.Persistencia;\nusing Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Migrations;\n\nnamespace ${config.espacio};\n\n[DbContext(typeof(${config.db}))]\n[Migration("${id}")]\n[RecursosMigracionSql("${recurso}")]\npublic sealed class ${clase} : Migration\n{\n    protected override void Up(MigrationBuilder migrationBuilder) =>\n        migrationBuilder.AplicarRecursosSql(typeof(${clase}));\n\n    protected override void Down(MigrationBuilder migrationBuilder) =>\n        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");\n}\n`,
    },
  ];
  for (const archivo of archivos) verificarRuta(raiz, archivo.ruta);
  // Un nombre ya usado se rechaza incluso con otro timestamp.
  const existentes = [
    ...listar(raiz, config.directorio),
    ...listar(raiz, `database/${config.schema}`),
  ];
  if (
    existentes.some(
      (ruta) =>
        new RegExp(`(?:^|/)\\d{14}_${clase}\\.(?:sql|cs)$`).test(ruta) ||
        (ruta.endsWith(".cs") && new RegExp(`\\bclass\\s+${clase}\\b`).test(leer(raiz, ruta))),
    )
  )
    throw new Error(`Colisión: ya existe ${clase}.`);
  const creados = [];
  if (!opciones["--dry-run"]) {
    try {
      for (const archivo of archivos) {
        const ruta = verificarRuta(raiz, archivo.ruta);
        mkdirSync(dirname(ruta), { recursive: true });
        writeFileSync(ruta, archivo.contenido, { flag: "wx" });
        creados.push(ruta);
      }
    } catch (error) {
      for (const ruta of creados) unlinkSync(ruta);
      throw error;
    }
  }
  console.log(JSON.stringify({ contexto, id, dryRun: !!opciones["--dry-run"], archivos }, null, 2));
});
