# E8b — propuesta de precisión de tarifas y costos técnicos

Estado: propuesta pendiente de autorización; no modifica contrato HTTP ni aplica aún una excepción de cálculo.

## Conflicto verificable

[Números y moneda](../rules/numeros-y-moneda.md) y [backend §18](../architecture/backend.md#18-representación-y-formato-de-datos-unificado) indican `Money.Round(minorUnits)` por línea y después el total, con `numeric(19,4)` como almacenamiento monetario general. USD tiene dos unidades menores en el catálogo. El plan T06 exige acumular costos por llamada, reservas y umbrales a partir de tarifas por millón, incluidas llamadas cuyo costo es menor a un centavo.

Con las tarifas Haiku aprobadas de USD 0,10 de entrada y USD 0,50 de salida por millón, una llamada con 1000 tokens de entrada y 100 de salida cuesta USD 0,00015. Redondear cada llamada a dos decimales guarda cero; 100 llamadas seguirían sumando cero aunque su costo técnico conjunto sea USD 0,015. Tampoco debe cambiarse una tarifa de caché de USD 0,125 por millón a USD 0,13 antes de calcular. Son estimaciones técnicas, no una factura del proveedor.

T02 ya mapea las tarifas y los agregados `CostAmount` como `numeric(18,6)`. Se conservan sus migraciones históricas; cualquier ajuste posterior se hace con una migración nueva. Seis decimales en el agregado tampoco representan el costo de un solo token de entrada Haiku (USD 0,0000001).

## Cambio propuesto para autorizar

- Mantener `Money` con moneda explícita y `decimal` en tarifas, snapshots, reservas y costos técnicos del asistente.
- Conservar las tarifas por millón con hasta seis decimales, como `numeric(18,6)`, sin redondearlas a centavos. Validar esa precisión al recibir una tarifa nueva y en referencias técnicas de host.
- Calcular y acumular los costos y reservas con hasta doce decimales (`numeric(24,12)`). Una tarifa con seis decimales multiplicada por tokens enteros y dividida por un millón requiere hasta doce decimales. Si se normaliza un costo técnico, usar `AwayFromZero` a esa precisión; no redondear cada llamada a los dos decimales de USD antes de acumularla.
- Comparar presupuesto y umbrales con el costo técnico acumulado y sus reservas a esa precisión. `budgetProgress` conserva su forma decimal aprobada; los costos desconocidos mantienen la estimación conservadora prevista por T06.
- Los presupuestos ingresados y la presentación de dinero siguen las unidades menores del catálogo. Correos y exportaciones usan `DisplayFormatter`; la excepción no se extiende a importes comerciales de otras áreas.
- Documentar el alcance en una ADR y en las dos fichas citadas; verificar acumulación de subcentavos, snapshots, persistencia y cruces de presupuesto mediante pruebas dirigidas y PostgreSQL Docker.

No agrega campos, rutas, permisos ni tipos Front. La autorización de CC01–CC03 no se interpreta como aprobación de esta excepción. Hasta decidirla, T03 puede continuar con configuración y referencias; el cálculo y reconciliación T06 dependen de esta resolución.
