## Why

Ochocientos ochenta y cuatro tests tocan el asistente y **ninguno vio jamás un byte producido por un modelo**. `ProveedorGuionado` devuelve el JSON que escribimos nosotros, y por eso mismo es la capa correcta para afirmar sobre lo que se le **mandó** al modelo —que la temperatura no viaja, que el prefijo no cambia entre turnos— y se queda tal como está. `ProveedorSimulado` devuelve un texto fijo que ni siquiera es una generación válida. Lo que queda sin cubrir es la mitad de vuelta: el parseo de `GeneradorDeSql.Interpretar`, el del redactor y el del reescritor nunca corrieron contra la salida real de un modelo.

**El punto de secuencia es lo que hace que este cambio vaya ahora y no después.** La primera corrida financiada del evaluador (TD-008, bloqueada por ARS-67) es un evento caro y único. Con el mecanismo de grabación puesto, esa corrida deja además N respuestas reales grabadas en disco que alimentan tests para siempre, y la línea de base sobrevive al presupuesto que la produjo. Sin él, deja un reporte y nada más, y grabar después cuesta otra corrida.

Es la misma jugada que las fixtures VCR de Metabase: si el cassette existe y no está la variable de re-grabación, se lee del disco; si no, se llama a la API real y se graba. Y se graba el **cuerpo crudo de la respuesta del proveedor**, no la respuesta ya procesada, para que el parseo del adaptador quede bajo test junto con todo lo que viene después.

## What Changes

- **Un `DelegatingHandler` de grabación en el pipeline del cliente HTTP del proveedor**, el mismo que `ModuleExtensions` arma con `AddHttpClient(ClienteDelProveedor)` y `ReintentoDeTransporte`. Intercepta el cable sin que `ProveedorAnthropic` se entere y **sin nombrar el SDK**, así que el guard de arquitectura que fija que el SDK viva en un solo archivo sigue en pie sin excepción nueva.
- **Clave de cassette determinista** por huella SHA-256 de (prefijo, mensaje, esfuerzo, modelo), leídos del cuerpo de la solicitud. Es el criterio que `ProveedorSimulado.Huella` ya usa, y por el motivo que ya está escrito ahí: `string.GetHashCode()` está aleatorizado por proceso.
- **Cassettes sellados** con modelo, fecha y hash del prefijo —el mismo sellado que RNF-03 le exige a los reportes de evaluación—, más el hash del fixture contra el que se grabaron.
- **Falla cerrado**: sin cassette y sin la variable de re-grabación, error ruidoso que nombra la clave faltante. **Nunca** una llamada de red en CI (RNF-15, RNF-16). Un cassette cuyo sello no coincide con el prefijo vigente **se rechaza en vez de servirse**.
- **Sin PII y sin credencial**: los cassettes se graban contra el fixture sintético y el sello lo declara; el cassette no guarda cabeceras de la solicitud, así que la clave del proveedor no puede filtrarse a disco. Los dos hechos quedan verificados por guard, no supuestos.
- **Los tests de parseo iteran el directorio de cassettes**, uno por cassette encontrado. El día que la corrida financiada deje cuarenta, son cuarenta casos nuevos sin escribir una línea de test.
- **Camino de grabación y reproducción probado punta a punta sin clave**, contra un transporte que impersona la API de mensajes.
- **TD-017** deja registrado el hueco que Metabase también tiene: una fixture congelada sin recaptura programada no detecta un cambio de formato de cable del proveedor.

## Capabilities

### New Capabilities

- `asistente-cassettes-del-proveedor`: grabación y reproducción del cuerpo crudo de la respuesta del proveedor en el pipeline HTTP del módulo, con clave determinista, sellado de identidad, falla cerrada sin red y garantías de que ningún cassette lleva datos personales ni la credencial.

### Modified Capabilities

<!-- Ninguna. Las capabilities del asistente todavía no están consolidadas en `openspec/specs/`: sus changes figuran completos pero sin archivar, así que no hay spec vigente que este cambio modifique. -->

## Impact

- **Módulo afectado**: `Modules.Asistente` únicamente. Se suman una pieza en `Infrastructure/`, su registración en `ModuleExtensions` y opciones nuevas en `OpcionesAsistente`.
- **Grafo de dependencias**: sin cambios. El módulo sigue referenciando solo `ArsDocendi.Shared`, y el guard `El_modulo_solo_referencia_ArsDocendi_Shared` lo sigue verificando.
- **API pública**: sin cambios. No se toca `Modules.Asistente.Contracts`, así que no hay consumidores cross-module que revisar.
- **Base de datos**: sin cambios de schema ni migraciones.
- **Frontend**: sin cambios.
- **Configuración**: dos opciones nuevas por ambiente —el directorio de cassettes y la variable de re-grabación—, las dos vacías por default. Con el default, el módulo se comporta exactamente como hoy.
- **Normativa institucional**: no aplica. El cambio no introduce ninguna regla de negocio reglamentaria, así que no hay `BR-*` que registrar.
- **Rollback**: dejar el directorio de cassettes sin configurar apaga el mecanismo entero sin tocar código; el handler no se registra y el pipeline queda como está hoy. Revertir el commit no deja estado que limpiar porque no hay migración ni dato persistido fuera del repositorio.

## Out of Scope

- **La corrida financiada del evaluador** (TD-008, bloqueada por ARS-67). Este cambio entrega el mecanismo y lo prueba; los cassettes de salida real llegan con esa corrida.
- **Grabar los fallos de transporte**. El reintento ya está cubierto por sus propios tests contra el cable; los cassettes graban la respuesta que el pipeline efectivamente usó.
- **Cualquier cambio en `ProveedorGuionado`**. Sigue siendo la herramienta para afirmar sobre lo que se le manda al modelo, y este cambio no la reemplaza ni la reduce.
- **Los cassettes del carril determinista y de la API del sistema**. Acá solo se graba el proveedor del modelo.
