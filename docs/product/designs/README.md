# Designs

Esta carpeta hospeda las **especificaciones de diseño UX/UI** del sistema y los exports/screenshots correspondientes.

## Estado: herramienta TBD

El equipo todavía no decidió qué herramienta de diseño usar (opciones evaluadas: Figma, Pencil/Claude Design, Penpot, otras). Por ahora:

- Las **especificaciones de diseño** se documentan en archivos `.md` siguiendo `_design-spec-template.md`.
- Los **diseños visuales** (cuando existan) se guardan según la herramienta que se elija — actualizar este README en ese momento.
- Los **screenshots / exports** para review sin herramienta MCP van en `exports/`.

## Cuando se decida la herramienta

Actualizar:

1. Este README con la herramienta elegida + setup.
2. `docs/product/design-principles.md` si la herramienta impone restricciones.
3. La skill `/add-feature` (`.claude/skills/add-feature/SKILL.md`) step "Inputs de diseño".
4. `.mcp.json` del proyecto (a crear) con el MCP server correspondiente.
5. Skill `visual-test` (a portar del template) adaptada al MCP elegido.
6. CLAUDE.md sección Skills disponibles agregando `visual-test`.

## Estructura

```
docs/product/designs/
├── README.md                                # este archivo
├── _design-spec-template.md                 # template para especificaciones de feature
├── <feature-kebab>-design-spec.md           # uno por feature con surface UI
└── exports/                                 # screenshots PNG/JPG para review humano
    └── <feature-kebab>/
        ├── desktop-aprobacion.png
        └── mobile-listado.png
```

## Mientras no haya herramienta

Las decisiones de diseño se toman iterativamente con el cliente. Para cada feature con surface UI:

1. Crear `<feature-kebab>-design-spec.md` desde el template.
2. Describir estados (loading, empty, error, success, awaiting-approval) en texto.
3. Si hay sketches a mano o capturas, ponerlos en `exports/<feature-kebab>/`.
4. Iterar con el cliente; documentar feedback en el spec.
