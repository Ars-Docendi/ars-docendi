# Designs

Especificaciones de diseño UX/UI y sus mockups.

- **Mockups**: archivos Pencil (`.pen`). `screens.pen` contiene las pantallas y `ui-lib.lib.pen` los componentes de `@ars-docendi/ui`. Para editarlos se usa la skill `pencil-design`.
- **Design specs**: un `<feature-kebab>-design-spec.md` por feature con superficie UI, creado desde `_design-spec-template.md`. Describe estados (carga, vacío, error, éxito) y decisiones que el código no explica. Se actualiza en el mismo PR que cambia la UX (AGENTS.md, regla #9).
- **Exports**: capturas PNG/JPG para review humano en `exports/<feature-kebab>/`.

Cuando el mockup y la implementación divergen, manda la implementación; la divergencia se anota en la design spec.
