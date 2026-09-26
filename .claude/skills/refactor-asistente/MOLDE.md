# Molde de los renglones

Cada renglón de la cola se expande a estos ocho bloques. Ninguno se omite; si uno no aplica,
se escribe por qué. Un bloque ausente se lee como olvido.

````markdown
# <ID> — <una línea: qué queda distinto cuando esto está hecho>

<Una o dos líneas de por qué existe y por qué está en ese lugar de la cola.>

## Precondición — es también el estado

```bash
<comandos, con el resultado esperado en comentario. Todos desde la RAÍZ del repo.>
```
````

Si no matchea, **el renglón ya está hecho**: reportá y pará. No lo ejecutes a ojo.

## Qué está mal

<2-6 líneas, con archivo:línea. Sin adjetivos. Termina con el MODO DE FALLA: qué pasa hoy
si esto no se arregla. Si el modo de falla es silencioso, decilo — es el argumento entero.>

## Test rojo primero ← solo si el renglón cambia comportamiento (invariante #9)

<El test, con su archivo destino y el vecino al que se pega. Termina siempre con:
«Correlo y confirmá el rojo antes del fix. Si pasa en verde, alguien ya lo arregló:
parás y reportás.»>
<Si NO cambia comportamiento: «No aplica: movimiento mecánico. La red es la suite
existente, verde antes y después.»>

## Qué hacer

<Archivos exactos. Snippets que compilen contra los helpers que el archivo REALMENTE tiene:
un snippet que inventa un método no compila y el agente lo improvisa.>

## Verificación

```bash
<el comando que prueba que está hecho + el que prueba que no rompiste nada>
```

## Docs en el mismo PR

<Invariante #6, o «no aplica» explícito.>

## No tocar

<Lista explícita. Siempre incluye: el guardarraíl que este renglón podría pisar, la mejora
adyacente que NO entra con su ID, y la decisión registrada que no se re-litiga.>

```

## Tres reglas de redacción

1. **Toda ruta y todo número de línea se verifica antes de escribirlo.** Una ruta inventada
   hace que el agente busque, encuentre otra cosa y la toque.
2. **"No tocar" es el bloque más importante.** Un agente sin límite explícito arregla lo que
   ve, y el PR pasa de 40 líneas a 400 y deja de ser revisable.
3. **El modo de falla va en "Qué está mal", no en el título.** "Mejorar X" no prioriza nada;
   "el asistente responde *no hay datos* cuando la respuesta es *no podés verlos*", sí.
```
