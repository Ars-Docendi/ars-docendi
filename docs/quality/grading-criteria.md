# Grading criteria

Rúbrica para evaluar trabajo en Ars Docendi. La usan reviewers humanos, agentes (`/evaluate`), y sirve como referencia para la **defensa de TFI**.

## Pesos

| Criterio                                          | Peso |
| ------------------------------------------------- | ---- |
| Funcionalidad / Compliance reglamentario          | 30%  |
| Calidad de código (arquitectura, layers, testing) | 25%  |
| Diseño / Experiencia de usuario                   | 20%  |
| Originalidad y craft                              | 15%  |
| Documentación (specs, BR-\*, arquitectura)        | 10%  |

## Escala (1–5) por criterio

### Funcionalidad / Compliance reglamentario (30%)

| Score | Descripción                                                                              |
| ----- | ---------------------------------------------------------------------------------------- |
| 5     | Cumple TODA la spec; tests cubren todas las BR-\* aplicables; flujos por rol verificados |
| 4     | Camino principal funciona, BR-\* críticas cubiertas, edge cases con minor gaps           |
| 3     | Camino principal funciona; gaps en autorización por rol o BR-\* secundarias              |
| 2     | Flujos principales rotos o autorización por rol con fugas                                |
| 1     | Funcionalidad central no funcional                                                       |

### Calidad de código (25%)

| Score | Descripción                                                                         |
| ----- | ----------------------------------------------------------------------------------- |
| 5     | Respeta layers, DAG, no fake UI, tests significativos, sin god classes              |
| 4     | Estructura sólida, minor violaciones (sin impacto en mantenibilidad)                |
| 3     | Aceptable; algunas violaciones de layer rules o cross-module sin justificar         |
| 2     | Violaciones serias de arquitectura (referencias a Internal/ de otro módulo, ciclos) |
| 1     | Código no mantenible; rompe invariantes del CLAUDE.md                               |

### Diseño / UX (20%)

| Score | Descripción                                                         |
| ----- | ------------------------------------------------------------------- |
| 5     | Cohesión visual, claridad, roles bien indicados, estados explícitos |
| 4     | Cumple design-principles; minor inconsistencias                     |
| 3     | Adecuado pero genérico; identidad débil                             |
| 2     | Disjoint; conflictos de estilo; IA confusa                          |
| 1     | Layout roto o jerarquía inutilizable                                |

### Originalidad y craft (15%)

| Score | Descripción                                                 |
| ----- | ----------------------------------------------------------- |
| 5     | Decisiones deliberadas y profesionales; sin clichés "AI UI" |
| 4     | Algo de custom; pequeños bolsones genéricos                 |
| 3     | Mayormente defaults/template                                |
| 2     | Obviamente template o patrones repetitivos                  |
| 1     | Boilerplate indistinguible                                  |

### Documentación (10%)

| Score | Descripción                                                                       |
| ----- | --------------------------------------------------------------------------------- |
| 5     | Spec actualizada, BR-\* completas con citas, arquitectura sincronizada con código |
| 4     | Docs presentes con minor gaps                                                     |
| 3     | Docs básicas pero desactualizadas en algunos puntos                               |
| 2     | Docs ausentes o ampliamente desactualizadas                                       |
| 1     | Sin documentación                                                                 |

## Threshold de aprobación

- **Default**: **ningún criterio < 3**, **funcionalidad ≥ 4** para releases al cliente.
- Funcionalidad < 4 → no se mergea a `main`.
- Excepciones documentadas en el plan activo y en `tech-debt.md`.

## Score compuesto

`0.30 × Func + 0.25 × Code + 0.20 × UX + 0.15 × Orig + 0.10 × Doc` (cada uno 1–5).

Registrar en `scorecard.md` después de cada feature relevante o cuando se corre `/evaluate`.
