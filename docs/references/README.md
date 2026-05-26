# References

Docs externas cacheadas en formato LLM-friendly para que agentes (y humanos) tengan referencias confiables sin depender de internet.

## Cómo agregar una referencia

1. Buscar la versión `llms.txt` o equivalente del proyecto/lib (muchos proyectos modernos exponen `https://<dominio>/llms.txt`).
2. Si no hay `llms.txt`, generarla a partir de la doc oficial (markdown plano, sin HTML, sin navegación).
3. Guardar como `<libreria>-<version>-llms.txt` en esta carpeta.
4. Mantener actualizada cuando se sube la versión en el proyecto.

## Naming

- `<libreria>-llms.txt` (versión latest del repo)
- `<libreria>-v<X>.<Y>-llms.txt` (versión específica si difiere de la latest)

## Referencias planeadas (a agregar a medida que aparezca necesidad)

- `dotnet-10-llms.txt` — APIs de .NET 10 relevantes
- `aspnetcore-10-llms.txt` — ASP.NET Core 10
- `efcore-10-llms.txt` — Entity Framework Core 10
- `react-19-llms.txt` — React 19
- `react-router-dom-7-llms.txt` — React Router DOM 7
- `tanstack-query-5-llms.txt` — React Query
- `vite-8-llms.txt` — Vite 8
- `azure-ad-msal-llms.txt` — Microsoft Authentication Library

## Cuándo NO agregar

- Para libs muy estables y bien conocidas (la doc en LLMs de entrenamiento suele bastar).
- Para snippets puntuales (eso va en specs o comentarios de código).

## Ver también

- [docs/architecture/stack.md](../architecture/stack.md) para versiones canónicas del stack.
