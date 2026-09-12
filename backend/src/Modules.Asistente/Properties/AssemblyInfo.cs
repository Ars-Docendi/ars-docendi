using System.Runtime.CompilerServices;

// Misma convención que ArsDocendi.Shared: los tests ven los internos del módulo.
// Es lo que permite probar el reintento de transporte y el proveedor simulado sin
// tener que hacerlos públicos solo para poder mirarlos.
[assembly: InternalsVisibleTo("ArsDocendi.IntegrationTests")]

// El núcleo del evaluador puntúa el pipeline, así que necesita sus tipos: el turno,
// la consulta generada y la abstención. Es la MISMA arista que
// `backend/manifiesto-de-aristas.json` ya registra como excepción del invariante #1
// con ticket ARS-63 — lo que cambia acá es que deja de pagarse haciendo público el
// módulo entero para que la vea un solo consumidor declarado.
//
// La superficie de `Application` es `internal` a propósito: `public` es una promesa
// de estabilidad, y el módulo no le prometió nada a nadie. Lo que sí ofrece a los
// otros módulos va en `Modules.Asistente.Contracts`, hoy vacío y que se llena con
// el carril determinista de API (ARS-10).
[assembly: InternalsVisibleTo("ArsDocendi.Evaluacion.Nucleo")]

// El ejecutable del evaluador, que compone el pipeline para correrlo. Va aparte del
// núcleo porque vive FUERA de la solución —`backend/eval/`, por el guardarraíl que
// separa lo que cuesta dinero— así que `dotnet build ArsDocendi.slnx` no lo compila
// y no avisa si esta línea falta. Se verifica compilando su .csproj a mano.
[assembly: InternalsVisibleTo("ArsDocendi.Evaluacion")]
