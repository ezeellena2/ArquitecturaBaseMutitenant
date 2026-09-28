Una `IEntityTypeConfiguration<T>` por tabla, agrupada por esquema. E2 configura las tablas globales y la auditoría; las demás se agregan en su etapa.
Leé: [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../../docs/rules/multitenancy.md).
Copiá de `Platform/DataProtectionKeyConfiguration.cs`; las tablas tenant, public_site y engagement requieren su RLS en la migración.
