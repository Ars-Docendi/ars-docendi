## 1. Leer el grafo real desde los `.csproj`

- [x] 1.1 `LectorDeAristas` — barre todos los `.csproj` de `backend/src`, sin filtrar por prefijo `Modules.`
- [x] 1.2 Resuelve el nombre del proyecto destino normalizando el separador de ruta de Windows que escriben los `.csproj`
- [x] 1.3 Usa `RaizRepositorio` en vez de escribir una décima búsqueda privada de raíz (TD-007)
- [x] 1.4 Test: el barrido encuentra los trece proyectos de `backend/src`, `ArsDocendi.Evaluacion.Nucleo` incluido
- [x] 1.5 Test: el barrido encuentra la arista `ArsDocendi.Evaluacion.Nucleo` → `Modules.Asistente`, que el glob de `ArquitecturaIdentityTests` no alcanza
- [x] 1.6 Test: un barrido que no encuentra ningún `.csproj` falla, en vez de pasar en verde con cero aristas
- [x] 1.7 Verificar en rojo: apuntando el barrido a un directorio vacío, 1.6 falla y 1.4 y 1.5 se quedan sin datos

## 2. El manifiesto: modelo y carga

- [x] 2.1 `ManifiestoDeAristas` — modelo de `proyectos` y `aristas`, cargado desde `backend/manifiesto-de-aristas.json`
- [x] 2.2 La clave de un proyecto es el nombre del `.csproj` sin extensión
- [x] 2.3 La carga falla si dos `.csproj` de `backend/src` comparten nombre: una clave que se degrada en silencio no es una clave
- [x] 2.4 La carga falla nombrando la arista si `origen`, `destino`, `via` o `motivo` están vacíos
- [x] 2.5 `via` acepta solo el vocabulario cerrado que el verificador sabe comprobar; hoy, `project-reference`
- [x] 2.6 Test: una arista sin motivo no carga y la nombra
- [x] 2.7 Test: una `via` fuera del vocabulario no carga y nombra la arista y la vía
- [x] 2.8 Test: dos proyectos homónimos hacen fallar la carga

## 3. El comparador y sus tres direcciones

- [ ] 3.1 `ComparadorDeAristas` — devuelve desviaciones tipadas, no un booleano
- [ ] 3.2 Dirección 1: arista presente en el código sin fila en el manifiesto
- [ ] 3.3 Dirección 2: fila en el manifiesto sin arista en el código
- [ ] 3.4 Dirección 3: proyecto de `backend/src` sin clasificar en el manifiesto, y proyecto declarado que ya no existe
- [ ] 3.5 Un proyecto que ninguna arista alcanza se declara `huerfano` y exige motivo escrito
- [ ] 3.6 Cada desviación nombra el objeto: origen y destino, o el proyecto
- [ ] 3.7 `ManifiestoDeAristasTests` en `backend/tests/ArsDocendi.IntegrationTests/Backend/`, junto a `ArquitecturaIdentityTests`
- [ ] 3.8 Test sintético: agregar una arista al código sin fila dispara la dirección 1
- [ ] 3.9 Test sintético: agregar una fila sin arista real dispara la dirección 2
- [ ] 3.10 Test sintético: un proyecto nuevo sin clasificar dispara la dirección 3
- [ ] 3.11 Test sintético: un proyecto huérfano sin motivo dispara una desviación
- [ ] 3.12 Test contra el repo real: las tres direcciones sobre el manifiesto y el código de verdad
- [ ] 3.13 Verificar en rojo: con el manifiesto todavía vacío, 3.12 falla con las trece filas de proyecto y todas las aristas sin declarar

## 4. La aciclicidad del grafo (invariante #2)

- [ ] 4.1 DFS sobre las aristas leídas del código, no sobre las declaradas en el manifiesto
- [ ] 4.2 Un ciclo se reporta enumerando los proyectos que lo forman
- [ ] 4.3 Test: el grafo real de `backend/src` es acíclico
- [ ] 4.4 Test sintético: una arista que cierra un ciclo lo detecta y lo enumera
- [ ] 4.5 Test: un ciclo en el código se detecta aunque el manifiesto declare un conjunto sin ciclos
- [ ] 4.6 Verificar en rojo: un DFS que no marca los nodos en curso hace fallar 4.4

## 5. Las excepciones son filas, no párrafos

- [ ] 5.1 `excepcion` opcional en una arista, con `invariante` y `ticket`
- [ ] 5.2 Una arista declarada como excepción sin `ticket` es una desviación
- [ ] 5.3 Una arista declarada como excepción sin motivo escrito es una desviación
- [ ] 5.4 Test: excepción sin ticket falla nombrando la arista
- [ ] 5.5 Test: excepción sin motivo falla nombrando la arista
- [ ] 5.6 Verificar en rojo: quitando el ticket a la excepción real del manifiesto, 5.4 falla

## 6. El manifiesto poblado con el grafo de hoy

- [x] 6.1 Enumerar los trece proyectos de `backend/src` con su estado
- [x] 6.2 `Modules.Asistente.Contracts` entra como `huerfano` con el motivo que hoy vive en un párrafo del documento
- [x] 6.3 Declarar todas las aristas reales con su motivo, migrando los motivos que la tabla del documento ya tenía
- [x] 6.4 Registrar `ArsDocendi.Host` → `ArsDocendi.Shared`, que faltaba en la tabla
- [x] 6.5 Registrar `ArsDocendi.Evaluacion.Nucleo` → `Modules.Asistente` como excepción, con el motivo del comentario de su `.csproj` y su ticket
- [x] 6.6 No escribir la fila `ArsDocendi.Host` → `Modules.Aulas/Portal/Tareas.Contracts`: es una fila de papel, ningún `.csproj` la referencia
- [x] 6.7 No escribir aristas proyectadas: las de ARS-46 llegan con ARS-46
- [x] 6.8 Correr la suite completa y verla en verde

## 7. Documentación

- [ ] 7.1 `docs/architecture/dependency-graph.md` — borrar la tabla del Edge registry y citar el manifiesto como fuente única
- [ ] 7.2 Rotular el diagrama Mermaid como dibujo de orientación, no normativo, con el puntero al manifiesto
- [ ] 7.3 Reescribir «Agregar un edge nuevo» como la edición del manifiesto, y decir que una excepción exige motivo y ticket
- [ ] 7.4 Mover al manifiesto el párrafo sobre `Modules.Asistente.Contracts` huérfano; el documento deja de repetirlo
- [ ] 7.5 Las secciones reescritas dicen «arista» (invariante #13)
- [ ] 7.6 `CLAUDE.md` — el invariante #2 nombra el manifiesto como el registro contra el que se chequea
- [ ] 7.7 `.claude/skills/architecture-drift-check/SKILL.md` — detección 2 (ciclos) y detección 5 (aristas no registradas) se apoyan en el test, no en un `grep` contra una tabla
- [ ] 7.8 `docs/quality/tech-debt.md` — el diagrama Mermaid no verificado y `backend/tests` fuera del barrido
- [ ] 7.9 `pnpm exec prettier --write` sobre los `.md` y el `.json` tocados
