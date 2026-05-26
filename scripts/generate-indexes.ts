#!/usr/bin/env tsx
/**
 * scripts/generate-indexes.ts — Genera _index.md en docs/{plans,product/specs,business-rules}/
 *
 * Lee el frontmatter YAML de cada .md (excepto _template.md, _index.md) y produce
 * tablas Markdown navegables. Los _index.md están en .gitignore — son artefactos
 * locales de navegación, no se versionan.
 *
 * Uso:
 *   pnpm exec tsx scripts/generate-indexes.ts
 *   pnpm generate-indexes
 */

import { readFileSync, readdirSync, writeFileSync, statSync, existsSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const ROOT = join(__dirname, "..");

type Frontmatter = Record<string, string>;

/** Parser mínimo de YAML frontmatter — soporta key: value de una línea. */
function parseFrontmatter(content: string): Frontmatter {
  const match = content.match(/^---\n([\s\S]*?)\n---/);
  if (!match) return {};
  const fm: Frontmatter = {};
  for (const line of match[1].split("\n")) {
    const kv = line.match(/^([\w_-]+):\s*(.*?)\s*$/);
    if (kv) {
      let value = kv[2];
      // strip quotes
      value = value.replace(/^["']|["']$/g, "");
      fm[kv[1]] = value;
    }
  }
  return fm;
}

function listMarkdownFiles(dir: string): string[] {
  if (!existsSync(dir)) return [];
  return readdirSync(dir)
    .filter((f) => f.endsWith(".md"))
    .filter((f) => !f.startsWith("_")) // skip _template, _bug-template, _index
    .map((f) => join(dir, f))
    .filter((p) => statSync(p).isFile());
}

interface IndexConfig {
  dir: string;
  title: string;
  columns: { key: string; label: string }[];
  emptyMsg: string;
}

const configs: IndexConfig[] = [
  {
    dir: "docs/plans/active",
    title: "Active plans index",
    columns: [
      { key: "__name", label: "Plan" },
      { key: "status", label: "Status" },
      { key: "module", label: "Módulo" },
      { key: "owner", label: "Owner" },
      { key: "started", label: "Started" },
      { key: "target", label: "Target" },
    ],
    emptyMsg: "_(No hay planes activos.)_",
  },
  {
    dir: "docs/plans/completed",
    title: "Completed plans index",
    columns: [
      { key: "__name", label: "Plan" },
      { key: "module", label: "Módulo" },
      { key: "owner", label: "Owner" },
      { key: "completed_at", label: "Completed" },
      { key: "pr", label: "PR" },
    ],
    emptyMsg: "_(No hay planes completados.)_",
  },
  {
    dir: "docs/product/specs",
    title: "Product specs index",
    columns: [
      { key: "__name", label: "Spec" },
      { key: "status", label: "Status" },
      { key: "module", label: "Módulo" },
      { key: "owner", label: "Owner" },
      { key: "last_updated", label: "Last updated" },
    ],
    emptyMsg: "_(No hay specs todavía.)_",
  },
  {
    dir: "docs/business-rules",
    title: "Business rules index",
    columns: [
      { key: "__name", label: "Módulo / archivo" },
    ],
    emptyMsg: "_(No hay business-rules todavía.)_",
  },
];

function generateIndex(cfg: IndexConfig): void {
  const absDir = join(ROOT, cfg.dir);
  const files = listMarkdownFiles(absDir);

  const lines: string[] = [];
  lines.push(`# ${cfg.title}`);
  lines.push("");
  lines.push(`> Autogenerado por \`scripts/generate-indexes.ts\` — no editar manualmente.`);
  lines.push("");

  if (files.length === 0) {
    lines.push(cfg.emptyMsg);
  } else {
    // Header
    lines.push("| " + cfg.columns.map((c) => c.label).join(" | ") + " |");
    lines.push("| " + cfg.columns.map(() => "---").join(" | ") + " |");

    // Rows
    for (const f of files) {
      const content = readFileSync(f, "utf-8");
      const fm = parseFrontmatter(content);
      const name = f.split("/").pop()!.replace(/\.md$/, "");
      const row = cfg.columns.map((col) => {
        if (col.key === "__name") {
          return `[${name}](./${name}.md)`;
        }
        return fm[col.key] ?? "—";
      });
      lines.push("| " + row.join(" | ") + " |");
    }
  }

  lines.push("");
  const outPath = join(absDir, "_index.md");
  writeFileSync(outPath, lines.join("\n"));
  console.log(`Generated ${relative(ROOT, outPath)} (${files.length} entries)`);
}

for (const cfg of configs) {
  generateIndex(cfg);
}
