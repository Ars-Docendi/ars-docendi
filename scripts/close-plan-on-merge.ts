#!/usr/bin/env tsx
/**
 * scripts/close-plan-on-merge.ts
 *
 * Disparado por .github/workflows/close-plan-on-merge.yml después de mergear un PR a develop.
 *
 * Lógica:
 *  1. Detectar qué archivos cambiaron en el merge (PR diff).
 *  2. Filtrar por docs/plans/active/<slug>.md.
 *  3. Si encontró EXACTAMENTE un plan tocado:
 *     - Leer el archivo + actualizar frontmatter (status: completed, completed_at, pr).
 *     - Llenar/iniciar sección Completion si no existe.
 *     - git mv a docs/plans/completed/<slug>.md.
 *     - Crear branch automation/close-plan-<slug>-pr-<NUMBER>, commit, push.
 *     - Abrir PR de docs hacia develop (review manual; sin auto-merge).
 *  4. Si encontró 0 planes o > 1: log y exit 0 (caso para manejo manual).
 *
 * Env vars requeridas (vienen del workflow):
 *  - PR_NUMBER, PR_URL, PR_TITLE, BASE_SHA, HEAD_SHA, GH_TOKEN
 */

import { execSync } from "node:child_process";
import { readFileSync, writeFileSync, existsSync, mkdirSync, mkdtempSync } from "node:fs";
import { join, dirname } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const ROOT = join(dirname(__filename), "..");

const PR_NUMBER = process.env.PR_NUMBER ?? "";
const PR_URL = process.env.PR_URL ?? "";
const PR_TITLE = process.env.PR_TITLE ?? "";
const BASE_SHA = process.env.BASE_SHA ?? "";
const HEAD_SHA = process.env.HEAD_SHA ?? "";

if (!PR_NUMBER || !BASE_SHA || !HEAD_SHA) {
  console.error("Missing required env vars: PR_NUMBER, BASE_SHA, HEAD_SHA");
  process.exit(1);
}

function run(cmd: string, opts: { cwd?: string } = {}): string {
  return execSync(cmd, {
    encoding: "utf-8",
    cwd: opts.cwd ?? ROOT,
    stdio: ["pipe", "pipe", "inherit"],
  }).trim();
}

// 1. Detectar archivos cambiados
console.log(`Inspecting merge: ${BASE_SHA}..${HEAD_SHA}`);
const changedFiles = run(`git diff --name-only ${BASE_SHA}..${HEAD_SHA}`)
  .split("\n")
  .filter(Boolean);

const touchedPlans = changedFiles.filter(
  (f) =>
    f.startsWith("docs/plans/active/") &&
    f.endsWith(".md") &&
    !f.endsWith("_template.md") &&
    !f.endsWith("_index.md"),
);

console.log(
  `Touched plans in active/: ${touchedPlans.length ? touchedPlans.join(", ") : "(none)"}`,
);

if (touchedPlans.length === 0) {
  console.log("No active plans touched. Exiting.");
  process.exit(0);
}

if (touchedPlans.length > 1) {
  console.log("Multiple active plans touched — leaving for manual handling via /complete-plan.");
  process.exit(0);
}

const planPath = touchedPlans[0]; // e.g. docs/plans/active/feature-x.md
const slug = planPath.replace(/^docs\/plans\/active\//, "").replace(/\.md$/, "");
const completedPath = `docs/plans/completed/${slug}.md`;

// 2. Asegurar que completed/ existe
if (!existsSync(join(ROOT, "docs/plans/completed"))) {
  mkdirSync(join(ROOT, "docs/plans/completed"), { recursive: true });
}

// 3. Leer + actualizar frontmatter + sección Completion
const absPlanPath = join(ROOT, planPath);
let content = readFileSync(absPlanPath, "utf-8");

const today = new Date().toISOString().slice(0, 10);

// Update frontmatter: status, completed_at, pr
function updateOrInsertFrontmatterField(content: string, key: string, value: string): string {
  const fmMatch = content.match(/^---\n([\s\S]*?)\n---/);
  if (!fmMatch) {
    // No frontmatter — insertar uno mínimo
    return `---\n${key}: ${value}\n---\n\n${content}`;
  }
  const fm = fmMatch[1];
  const keyRe = new RegExp(`^${key}:\\s*.*$`, "m");
  let newFm: string;
  if (keyRe.test(fm)) {
    newFm = fm.replace(keyRe, `${key}: ${value}`);
  } else {
    newFm = `${fm}\n${key}: ${value}`;
  }
  return content.replace(fmMatch[0], `---\n${newFm}\n---`);
}

content = updateOrInsertFrontmatterField(content, "status", "completed");
content = updateOrInsertFrontmatterField(content, "completed_at", today);
content = updateOrInsertFrontmatterField(content, "pr", PR_URL);
content = updateOrInsertFrontmatterField(content, "last_updated", today);

// Append (or update) Completion section
const completionSection = `

## Completion

- **Fecha**: ${today}
- **PR**: [${PR_TITLE}](${PR_URL}) (#${PR_NUMBER})
- **Outcome**: _(completar manualmente — describir qué se entregó realmente)_
- **Variaciones del plan original**: _(completar manualmente)_
- **Follow-ups**: _(linkear a items en backlog o tech-debt si aplica)_
`;

if (!/^##\s+Completion\b/m.test(content)) {
  content = content.trimEnd() + completionSection;
} else {
  console.log("Completion section already exists — leaving as is.");
}

writeFileSync(absPlanPath, content);

// 4. git mv
// Usamos --local explícito para dejar claro que escribimos en .git/config del repo,
// no en config global del runner (defensive, aunque el default ya es local).
run(`git config --local user.email "actions@github.com"`);
run(`git config --local user.name "github-actions[bot]"`);
run(`git mv ${planPath} ${completedPath}`);

// 5. Branch + commit + push
const branchName = `automation/close-plan-${slug}-pr-${PR_NUMBER}`;
run(`git checkout -b ${branchName}`);
run(`git add ${planPath} ${completedPath}`);
run(`git commit -m "docs: complete plan ${slug} after PR #${PR_NUMBER} merge"`);
run(`git push -u origin ${branchName}`);

// 6. Abrir PR hacia develop (sin auto-merge: review humano del cierre)
const prBody = `Auto-cierre del plan \`${slug}\` después del merge de [#${PR_NUMBER}](${PR_URL}).

- Movido a \`docs/plans/completed/${slug}.md\`
- Frontmatter actualizado: \`status: completed\`, \`completed_at: ${today}\`, \`pr: ${PR_URL}\`
- Sección \`## Completion\` agregada (revisar y completar manualmente si aplica)

_Generado por \`.github/workflows/close-plan-on-merge.yml\`._
`;

// Pasamos el body via --body-file con tempfile para evitar problemas de shell escape
// si PR_TITLE/PR_URL contienen caracteres especiales (comillas, backticks, $).
const tmpDir = mkdtempSync(join(tmpdir(), "close-plan-"));
const bodyFile = join(tmpDir, "pr-body.md");
writeFileSync(bodyFile, prBody);

run(
  `gh pr create --base develop --head ${branchName} --title "docs: complete plan ${slug} (#${PR_NUMBER})" --body-file ${bodyFile}`,
);

console.log(`PR de cierre abierto para plan ${slug}.`);
