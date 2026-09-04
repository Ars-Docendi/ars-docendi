using System.Runtime.CompilerServices;

// Misma convención que ArsDocendi.Shared: los tests ven los internos del módulo.
// Es lo que permite probar el reintento de transporte y el proveedor simulado sin
// tener que hacerlos públicos solo para poder mirarlos.
[assembly: InternalsVisibleTo("ArsDocendi.IntegrationTests")]
