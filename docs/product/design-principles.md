# Design principles

Esta guía orienta las decisiones de **UX/UI** y **identidad visual** del sistema. Los evaluadores la referencian junto con `docs/quality/grading-criteria.md`.

## Principios

1. **Cohesión institucional** — Tipografía, color, espaciado y motion deben sentirse como una herramienta del Departamento de Ingeniería UNLaM, no como una colección de pantallas dispersas.
2. **Claridad sobre decoración** — Acciones primarias evidentes para cada rol; sin ruido ornamental. Los usuarios institucionales priorizan eficiencia, no "delight".
3. **Defaults accesibles** — Contraste suficiente, focus visible, hit targets adecuados, escala tipográfica legible. El sistema lo usan docentes de distintas edades y niveles de comodidad digital.
4. **Roles visibles, permisos claros** — Cada vista deja claro qué rol está viendo y qué puede hacer. Sin botones fantasma que aparezcan/desaparezcan sin explicación según el rol.
5. **Estados explícitos** — Loading, vacío, error, éxito, y _en espera de aprobación_ son estados de primera clase con su propio diseño (no spinners genéricos).

## Anti-patterns (flag en review)

- **Lorem ipsum** o copy "TODO" en flujos visibles al cliente.
- Controles que **aparentan funcionar pero no hacen nada** (stub interactions).
- **Inconsistencia** de espaciado/tipografía entre módulos sin sistema declarado.
- **Modal-fatiga**: usar modales donde un panel inline o página dedicada sería más clara.
- **Jerga técnica** filtrada al usuario final (mostrar IDs de DB, mensajes de error con stacktrace, traducciones literales de excepciones).
- **Iconos sin label** en acciones críticas (un docente no debe adivinar qué hace un ícono solo).

## Plataforma

- **Web (React + Vite)**: layouts responsivos. Respetar `prefers-reduced-motion` en animaciones. Considerar uso desde tablet/notebook en oficina y desde móvil ocasionalmente.
- **Print-friendly** donde corresponda: secretaría puede necesitar imprimir designaciones aprobadas, listados de reservas.

## Tono de copy

- **Profesional, claro, sin tutorial**: el usuario sabe su rol institucional; no es necesario explicarle qué es una designación.
- **Mensajes de error accionables**: en vez de "Error 500", "La aprobación no se pudo registrar. Reintentá o contactá a Secretaría Académica si persiste."
- **Idioma**: español rioplatense neutro institucional (no slang, no "vos" informal en mensajes del sistema; sí "vos" en confirmaciones del docente cuando aplique tono cercano).
