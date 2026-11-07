# Cómo se comportaba el asistente antes de aulas y portal

Las cuatro líneas de base congeladas el 2026-11-07 (mañana) con el prefijo
`9ae0bab9468ee2df…`, que ya tenía `comisiones` pero no las solicitudes de aula, los
proyectos del portal ni la regla de columnas clave de los enlaces.

No las mira ningún gate: su sello ya no coincide con el prefijo vigente.

| Eje       | Ítems | Aciertos | Normalizado |
| --------- | ----- | -------- | ----------- |
| Capacidad | 50    | 49       | 98,0 %      |
| Robustez  | 18    | 18       | 100,0 %     |
| Diálogo   | 11    | 9        | 81,8 %      |
| Social    | 20    | 20       | 100,0 %     |

## Qué se movió

| Ítem                                | Antes                       | Ahora                       | Por qué                                                      |
| ----------------------------------- | --------------------------- | --------------------------- | ------------------------------------------------------------ |
| `cap-004`                           | traducción correcta         | se abstuvo ante contestable | Oscila (2 de 3 corridas aciertan); está en `inestables.json` |
| `cap-020`                           | intentó lo infactible       | abstención correcta         | Oscila; ya estaba en `inestables.json`                       |
| `cap-032`                           | abstención correcta         | traducción correcta         | Reclasificado: los proyectos del portal ahora se conceden    |
| `dia-001-seguimiento#2`             | se abstuvo ante contestable | traducción correcta         | Oscila                                                       |
| `dia-005-referencia-al-resultado#2` | se abstuvo ante contestable | traducción correcta         | Oscila                                                       |

Los ítems nuevos (`cap-051`..`cap-061`, `rob-019`..`rob-022`) no tienen «antes».
