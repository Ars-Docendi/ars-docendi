# Architecture

Esta carpeta describe **cómo** está estructurado Ars Docendi: decisiones de stack, fronteras de módulos, APIs y datos.

## Orden de lectura

1. [stack.md](./stack.md) — tecnologías y rationale
2. [module-anatomy.md](./module-anatomy.md) — layout y reglas de capas de un módulo .NET
3. [dependency-graph.md](./dependency-graph.md) — dependencias permitidas (DAG)
4. [api-contracts.md](./api-contracts.md) — fronteras HTTP / API pública
5. [data-model.md](./data-model.md) — persistencia y entidades
6. [infrastructure.md](./infrastructure.md) — topología de ambientes y deploy
7. [frontend-remote-data.md](./frontend-remote-data.md) — datos remotos y locales en el frontend

## Docs por dominio

Para cada módulo backend (Designaciones, Aulas, Portal, Tareas), se mantiene `domains/<dominio>.md` (a partir de `_template.md`).

## Relacionados

- Skill: `/create-module` (`.claude/skills/create-module/SKILL.md`)
- Calidad: [docs/quality/golden-principles.md](../quality/golden-principles.md)
- Reglas de negocio: [docs/business-rules/](../business-rules/)
