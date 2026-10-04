Convenciones EF del modelo: `IVersioned` se mapea a `xmin`, `Email` se persiste como valor canónico y `PhoneNumber` como su E.164 en `varchar(16)`.

Leé: [concurrencia](../../../../docs/rules/concurrencia.md) · [emails](../../../../docs/rules/emails.md) · [persistencia-ef](../../../../docs/rules/persistencia-ef.md). Las prueba `VersionedModelTests`.
