Entidades de prueba para las tres clases de datos. La fixture las agrega al modelo derivado y crea sus tablas con `IsolationSchema` después de migrar, nunca en una migración productiva.
Leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [tests](../../../../docs/rules/tests.md).
Copiá de `Widget.cs` para dato privado, `Poster.cs` para público y `Deal.cs` para compartido.
