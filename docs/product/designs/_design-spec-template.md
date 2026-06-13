---
status: draft # draft | review | approved
owner: ""
feature: "" # link al spec en openspec/specs/<capability>/spec.md
last_updated: YYYY-MM-DD
---

# Design spec: &lt;feature-name&gt;

## Resumen

Un párrafo: qué experiencia se diseña y para qué rol(es).

## Roles que ven esta surface

- [ ] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [ ] Administrativos
- [ ] Docente

## Flujo principal

Descripción del happy path paso a paso, desde que el rol entra a la página hasta que completa la acción.

1. _(paso 1)_
2. _(paso 2)_
3. _(paso 3)_

## Layout / IA

Descripción del layout principal. Si hay sketches o mockups, linkear:

- Sketch / mockup desktop: `exports/<feature>/desktop-<state>.png`
- Sketch / mockup mobile (si aplica): `exports/<feature>/mobile-<state>.png`

## Estados a diseñar

| Estado            | Descripción                          | Cuándo se muestra                    |
| ----------------- | ------------------------------------ | ------------------------------------ |
| Loading           | _(qué se ve mientras cargan datos)_  | Carga inicial, refetch               |
| Empty             | _(qué se ve si no hay datos)_        | Cuando el listado está vacío         |
| Error             | _(qué se ve si falla)_               | Errores de network, server, permisos |
| Success           | _(qué se ve cuando todo OK)_         | Estado normal con datos              |
| Awaiting approval | _(qué se ve si requiere aprobación)_ | Estado especial del workflow         |

## Decisiones de diseño

- _(decisión y motivo)_

## Anti-patterns a evitar (específicos de esta feature)

- _(qué NO hacer y por qué)_

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Spec funcional: [`openspec/specs/<capability>/spec.md`](../../openspec/specs/)

## Open questions de diseño

- _(cualquier duda sin resolver con el cliente)_
