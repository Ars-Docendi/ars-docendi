# Cómo se comportaba el asistente antes de portal

Las cuatro líneas de base congeladas **antes** de conceder el schema `portal`, con
el prefijo `8e3013fe0116e538…` y 14 tablas.

No las mira ningún gate: su sello ya no coincide con el prefijo vigente y el gate
se niega a comparar, que es lo correcto. Están acá porque son **el único punto de
comparación que va a existir** para responder qué costó abrir portal, y la
respuesta concreta fue:

| Eje       | Empeoraron | Mejoraron |
| --------- | ---------- | --------- |
| Capacidad | `cap-004`  | uno       |
| Robustez  | `rob-011`  | —         |
| Diálogo   | —          | —         |
| Social    | —          | —         |

Dos ítems, los dos pasando de traducción correcta a abstención, sobre un prefijo
que creció de 14 a 20 tablas. Sin esta foto, «capacidad pasó de 95,8 % a 96,9 %
con ocho ítems más» no se podía interpretar: no se sabría si el asistente mejoró,
empeoró, o si los ítems nuevos taparon una caída.

Congelarlas costó cero porque los cassettes de entonces todavía servían. Sacar la
misma foto hoy cuesta una corrida financiada.
