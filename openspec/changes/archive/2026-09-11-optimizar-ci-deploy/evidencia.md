# Evidencia de ejecución

## Línea base

Comando: `dotnet test backend/ArsDocendi.slnx --no-build -c Release --verbosity normal`

- Tests: 148
- Fallos: 0
- Omitidos: 0
- Duración del runner: 3m29s365ms
- Duración total informada: 3m29s654ms

## Fixture de assembly

Comando: `dotnet test backend/ArsDocendi.slnx --no-build -c Release --verbosity normal`, repetido tres veces después de recompilar Release.

| Ejecución | Tests | Fallos | Omitidos | Duración total |
| --------- | ----: | -----: | -------: | -------------: |
| 1         |   151 |      0 |        0 |     1m37s039ms |
| 2         |   151 |      0 |        0 |     1m35s347ms |
| 3         |   151 |      0 |        0 |     1m36s009ms |

Mediana: **1m36s009ms**. Reducción contra la línea base: **54,2%**.

La DLL existente usada para la línea base reportó 148 casos; la recompilación de la fuente actual descubre 151. El diff de esta change sólo modifica el fixture y las anotaciones de colección, no elimina ni agrega declaraciones de tests. La discrepancia queda registrada para no confundirla con una reducción de cobertura.

Las ejecuciones necesitaron acceso al socket Docker para Testcontainers; el runner sandbox no lo expone.

## Restore y deploy

`time dotnet restore ArsDocendi.slnx -m:1 -v minimal`, desde `backend/`, terminó correctamente con `real 0m3.043s`. Es una medición local con los paquetes globales ya disponibles; no mide el servicio remoto de GitHub Actions ni permite distinguir hit/miss de `actions/cache`.

No se ejecutó un deploy real: este runner no tiene secrets, registro de imágenes ni runner self-hosted de deploy. No hay duración de deploy comparable disponible. La revisión del diff confirma que los pasos de build, push, reset, `spin-up`, tags y teardown no cambiaron; la única mejora medida de wall-clock proviene del fixture de assembly y su paralelismo.

No se modificaron `backend/src/` ni `docs/architecture/dependency-graph.md`; tampoco se eliminaron declaraciones de tests. El conteo 151 de la fuente recompilada queda separado del conteo 148 de la DLL usada como línea base.
