using System.Runtime.CompilerServices;

// Misma convención que ArsDocendi.Shared: los tests ven los internos del módulo.
[assembly: InternalsVisibleTo("ArsDocendi.IntegrationTests")]

// El núcleo del evaluador puntúa el pipeline, así que necesita sus tipos: el turno, la consulta
// generada y la abstención.
[assembly: InternalsVisibleTo("ArsDocendi.Evaluacion.Nucleo")]

// El ejecutable del evaluador, que compone el pipeline para correrlo. Va aparte del núcleo porque
// vive FUERA de la solución —`backend/eval/`, por el guardarraíl que separa lo que cuesta dinero—
// así que `dotnet build ArsDocendi.slnx` no lo compila y no avisa si esta línea falta.
[assembly: InternalsVisibleTo("ArsDocendi.Evaluacion")]
