# 0051 · Precisión técnica de tarifas, reservas y costos de IA

**Estado:** aceptada por el usuario, 2026-10-10. Implementación y verificación en T03/T06 de E8b.

## Contexto

La regla monetaria general redondea cada línea y el total a las unidades menores del catálogo. El asistente estima costos por tokens con tarifas por millón; una llamada puede costar menos que un centavo. Redondear cada llamada a centavos antes de acumularla elimina consumo y permite eludir el presupuesto y sus alertas.

Con las tarifas Haiku aprobadas de USD 0,10/0,50 por millón, 1000 tokens de entrada y 100 de salida cuestan USD 0,00015. Cien llamadas acumulan USD 0,015; redondear cada una a dos decimales produce cero. La tarifa de escritura de caché USD 0,125 por millón también debe conservarse antes de calcular.

El usuario autorizó expresamente la [propuesta concreta](0048-asistente.md): «Sí, autorizar precisión técnica y documentarla». La excepción se limita a estimaciones técnicas de IA, sin cambiar contrato HTTP ni tipos Front.

## Decisión

- Tarifas por millón y sus snapshots: `Money`, `decimal`, moneda explícita, hasta seis decimales; almacenamiento `numeric(18,6)`. Una tarifa nueva o una referencia técnica con mayor escala se rechaza; no se trunca ni se redondea silenciosamente a centavos.
- Costos y reservas técnicos, por llamada y acumulados: `Money`, `decimal`, hasta doce decimales; almacenamiento `numeric(24,12)`. Una tarifa con seis decimales, tokens enteros y divisor de un millón requiere hasta doce decimales. La normalización técnica usa `AwayFromZero` a esa escala, conservando subcentavos al acumular. No se aplica `MinorUnits` por llamada antes de sumar estos costos.
- Los umbrales y el presupuesto disponible usan los costos y reservas técnicos persistidos. Las reservas no se confunden con consumo para disparar alertas; el uso incierto conserva la estimación conservadora prevista por T06. No son importes de una factura de Anthropic.
- Los presupuestos ingresados se redondean en el backend a los `MinorUnits` obtenidos del catálogo, con `AwayFromZero`. La presentación de dinero, correos y exportaciones conserva `DisplayFormatter` y las unidades menores; el Front sólo muestra los valores y las proyecciones provistos por el backend.
- El módulo sigue validando referencia habilitada para un dato monetario nuevo y leyendo datos históricos si la moneda se deshabilita. No se agrega un catálogo de monedas ni se pierde la moneda al calcular.

Las migraciones históricas T02 se conservan. El ajuste de columnas de costos y las columnas nuevas de reservas se aplican en una migración posterior T06. Esta excepción no se extiende a importes comerciales, saldos, pagos ni otras áreas, y no crea una variante del contrato `{ amount, currency }`.

## Alternativas descartadas

Redondear cada llamada a centavos falsea el consumo. Acumular `decimal` sin moneda viola la regla de `Money`. Usar `double` introduce errores binarios. Mostrar más decimales en el Front no recupera consumo eliminado por el backend. Reescribir la migración T02 impide aplicar la actualización sobre bases ya migradas.

## Consecuencias y verificación

T03 verifica tarifas con seis decimales, rechazo de escala excesiva sin truncamiento y presupuestos según las unidades menores del catálogo. T06 verifica costos por token, acumulación de subcentavos, conservación de snapshots, reservas concurrentes y cruces 80/100; la persistencia se prueba con PostgreSQL Docker. No se declara ese cálculo implementado por publicar esta decisión.

La regla operativa queda en [números y moneda](../rules/numeros-y-moneda.md) y [backend §18](../architecture/backend.md#18-representación-y-formato-de-datos-unificado). El contrato aprobado CC01–CC03 conserva sus campos, rutas, permisos y tipos.
