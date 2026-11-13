# Cómo se comportaba el asistente antes de comisiones

Las cuatro líneas de base congeladas **antes** de conceder el schema `comisiones`,
con el prefijo `1d2ba01c2e54f286…`. Son las que se regeneraron con la corrida que
regrabó los cassettes contra el prefijo que pide `motivo` y `termino` (ARS-139,
unidad B; `7e06fddb…` y sucesores), congeladas reproduciendo esos cassettes.

No las mira ningún gate: su sello ya no coincide con el prefijo vigente. Están acá
como punto de comparación para responder qué costó abrir comisiones.

| Eje       | Ítems | Aciertos | Normalizado |
| --------- | ----- | -------- | ----------- |
| Capacidad | 39    | 38       | 97,4 %      |
| Robustez  | 15    | 15       | 100,0 %     |
| Diálogo   | 11    | 11       | 100,0 %     |
| Social    | 20    | 20       | 100,0 %     |

El que no acertaba en capacidad era `cap-008` («¿Qué solicitudes puedo ver de mi
carrera?»): el modelo se abstenía creyendo que «de mi carrera» exige filtrar por la
identidad del usuario, cuando la RLS ya acota las filas.

## Qué se movió con comisiones

Comparando estas contra las vigentes, ítem por ítem:

| Ítem                                | Antes                       | Ahora                       | Por qué                                                               |
| ----------------------------------- | --------------------------- | --------------------------- | --------------------------------------------------------------------- |
| `cap-008`                           | se abstuvo ante contestable | traducción correcta         | Mejoró en las cuatro corridas en vivo                                 |
| `cap-019`                           | abstención correcta         | traducción correcta         | Reclasificado como contestable: el aula de cada comisión ahora se lee |
| `cap-020`                           | abstención correcta         | intentó lo infactible       | Oscila: ver la tabla de ítems inestables del README                   |
| `dia-001-seguimiento#2`             | traducción correcta         | se abstuvo ante contestable | Oscila                                                                |
| `dia-005-referencia-al-resultado#2` | traducción correcta         | se abstuvo ante contestable | Oscila                                                                |

Los ítems nuevos (`cap-040`..`cap-050`, `rob-016`..`rob-018`) no tienen «antes».
