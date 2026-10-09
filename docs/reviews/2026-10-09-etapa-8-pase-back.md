# Etapa 8 · Continuidad del pase en Back

**Estado:** código Back estabilizado en `8e0bb63c`; build completo limpio y segunda suite completa con Docker **2262/2262, exit 0**. E8/T14 abiertas. Sólo Back, main, sin push ni cambios propios en Front; AppHost, secretos y Meta quedaron a cargo de Integración.

## Contrato y encajes coordinados

- `5358f6967f16eb0fc372bc3d6c553a0fa723d3f2`: CTA `/registro?channel=whatsapp` y limpieza de descripciones/501 temporales de endpoints implementados. Comparación estructural contra T01 idéntica al excluir únicamente esa metadata; tres puertos neutrales sin diff. Build focal limpio; contrato 11/11, bot 9/9 y OpenAPI 15/15.
- OpenAPI SHA-256 normalizado LF: `cf80064358a63daa3be361dc4a5308d0ee52e27a31824953061eb1d44b77727d`. Front regeneró tipos en `5dfde077`, SHA LF `70ff361a5c4079f19ffed7e702592eca680f76b9e88ddb3e7647815caf3fac93`. `contracts:check` ejecutado desde Back: exit 0, tipos coincidentes.
- Front completó CTA en `8b428edf` y cancelación de baja desde enlace en `9ac6b8bb`; conserva retorno consumer fijo `/login/enlace` y comienza OIDC nuevo sin segundo canje. Acuerdos registrados en «Cambios al contrato» del plan.

## Soporte del navegador permanente

`f2ec23a733bbee74dc767b6eecfb486d954ecc17` versiona [la fixture y su protocolo](../guides/e2e-whatsapp.md). Reutiliza ApiFactory/RealE2ESetup: Kestrel HTTPS, PostgreSQL Testcontainers y outbox cifrado reales; control exclusivamente por IPC privado ligado a escenario/destinatario/cursor UTC. Incluye OTP del operador, evento firmado, eventos persistidos y vencimiento de un ticket exacto. Transportes externos bloqueados; ningún endpoint de control o lectura OTP productivo.

Guardas y persistencia 15/15. Smoke del comando real: alta Phone/OTP, webhook firmado persistente y deduplicado, procesamiento/enlace del bot y OTP Email del operador correctos; contenedor y certificados propios descartados. El runner permanente y aprobación/rechazo por la UI real de Plataforma pertenecen al Front/Integración; una fixture aprobada no sustituye ese recorrido ni Meta.

`25983e9fb6f3271536e3ea512977410d1f2ad39b` excluye el tool del descubrimiento MTP mediante `IsTestingPlatformApplication=false`; `IsTestProject=false` solo no alcanzaba por la referencia transitiva a xUnit.

## Verificación y documentación

- Primera corrida completa: **2262/2262 tests correctos, 0 fallos de tests y 0 omitidos**, 20m35s; **exit 1** porque el SDK intentó ejecutar el tool como un test. Ese fallo externo se conserva y fue corregido, sin omitir pruebas.
- Build completo posterior a la centralización: **0 advertencias, 0 errores**, 41.92s. WhatsApp **143/143**, catálogo de errores **8/8**; SHA OpenAPI idéntico.
- `8e0bb63c1638b7e5a5e52d89c28de91562b24cf6` centraliza dos códigos ya existentes en `Domain/WhatsApp/WhatsAppErrors`, conservando status, descripción, retryAfter y contrato; resuelve sus falsos positivos documentales sin borrar códigos ni relajar el detector.
- General documentado en `53f2175e`; arnés mecánico verificado/fusionado en `84d27d78`. CLI npm falló sin resultado; CLI de la app aplicó 11 automáticos. Dos ejecuciones del modelo dejaron cuatro altas del árbol y los dos códigos anteriores sin resolver. Detector tras centralización: **0 auto, 4 modelo, 0 humano**; **documentación todavía no está en cero**. Las cuatro entradas del árbol quedaron en la coordinación de la excepción manual a la skill.
- Segunda suite completa: **2262/2262 correctos, 0 fallos, 0 omitidos, exit 0**, 16m46s, sobre los cuatro proyectos reales de tests. El tool quedó correctamente fuera del descubrimiento; ninguna prueba se excluyó. Logs locales: `.local/reviews/2026-10-09-pase-back-build-final.log`, `2026-10-09-pase-back-test-full.log` y `2026-10-09-pase-back-test-final.log`.

## Lo que conserva Integración

Runner permanente Front, documentación en cero, galería/recorrido con sesiones anteriores y recorrido Meta real. Front informó paquete estático `cab63db6`, runner `579fc2c3` con 30 grupos (15 por viewport), focales 12/12, full 2350/2350, build/lint/contracts en 0 y galería 108/108; no se presenta como una ejecución del runner real. Back libera fuentes y binarios para que Integración lo ejecute después de esta suite. La [coordinación operativa](2026-10-09-etapa-8-coordinacion.md) registra plantillas, categorías y pendientes de origen/receptor; este pase no cambia callback ni envía a Meta.

La preparación inicial de T14 fue sustituida por la [prueba efectiva de retiro](2026-10-09-etapa-8-retiro-whatsapp.md), en copia del commit `605be253` y bases propias existente/vacía: build 0 advertencias/errores, suite completa **2084/2084, 0 omitidos, exit 0**, datos y esquema del núcleo preservados, reversión estructural con **12/12 grants** individuales y contenedor propio eliminado. La migración Drop permanece sólo en esa copia, sin bases compartidas ni retiro en main. El informe registra los ajustes precisos de tests de la instalación retirada; la prueba no acredita Meta ni cierra E8.
