# E8b T03 · Configuración, modelos y claves

Implementación Back de T03 sobre T02 `fa74d44768187b40e6aba175aa1f1fe2a2babefb` y contrato CC01–CC03 aprobado. No cierra E8/E8b ni acredita un recorrido con Anthropic o Meta reales.

## Conducta implementada

La configuración y catálogo se leen sin clave de proveedor. Plataforma puede editar modelos, selección, esfuerzo, límites, retención y habilitación Personal. Business devuelve las dos listas de modelos elegibles independientes del modo guardado, aun si la selección persistida quedó invalidada. PlatformKey admite sólo activos compatibles permitidos; OwnKey todos los activos compatibles si la política lo permite. Los límites PlatformKey se muestran y rechazan ediciones desde Business. Las lecturas son vivas, sin caché de configuración/modelos; una revocación no habilita otro modelo ni cambia el financiamiento.

Las claves se validan mediante petición mínima fuera de UOW y se guardan con versión. `keep` conserva, `replace` cifra y `clear` guarda un tombstone que impide revivir el secreto de arranque. OwnKey se cifra con propósito específico por organización y requiere Dueño de sistema con cuenta y membresía activas y método Email verificado utilizable. Phone y contacto Google no satisfacen el requisito. Un método administrado por otra organización es utilizable si su membresía y esa organización siguen activas, sin exigir rol en la administradora; la consulta usa el índice global autorizado y no saltea RLS de Members. El canal Email obligatorio del núcleo está siempre disponible en `EmailLoginCodeChannel`; no se inventa una condición SMTP distinta de la política existente.

El SDK oficial se crea al usar una llamada, con esfuerzo explícito, adaptive y sin reintentos. La validación de clave usa Haiku y 64 tokens máximos de salida; errores de autorización y transitorios se sanitizan. El adaptador de chat y streaming tampoco entrega mensaje, cuerpo o inner exception del SDK. Cada cliente posee y libera el SDK y transporte: el `Dispose` de `AsIChatClient` solo no libera esos recursos en SDK 12.55. Fake se limita a Testing/E2E explícitamente aislado y no se selecciona mediante body/header.

## Decisiones tomadas

- Al cambiar a PlatformKey se conservan los topes y la clave propios; ese modo lee los límites globales vigentes. Volver a OwnKey con topes omitidos recupera los guardados, incluido presupuesto cero. La primera configuración OwnKey sin presupuesto previo recibe el default inicial, aun si ya existía una fila PlatformKey; se valida moneda habilitada y normaliza fuera de UOW sólo para el monto nuevo.
- `AssistantSettingsSelectionPolicy` comparte modelo/default/permitidos/esfuerzo entre el preflight anterior a validar la clave y el chequeo dentro de UOW. Una selección local inválida no llama al SDK; revocaciones durante esa llamada todavía impiden guardar. La retención se edita en Plat-Config y se lee global vigente en ambos modos, sin depender del último guardado OwnKey ni modificar la columna legacy.
- El seed del módulo es un contribuidor neutral ejecutado dentro de la única UOW/lock de `DatabaseSeeder`, con SecurityEvent global de sistema; Core no importa Assistant. Inicializa sólo cuatro referencias oficiales embebidas y no pisa modelos/settings existentes. Un seed sin cambios permite monedas históricas deshabilitadas.
- El catálogo de referencias compatibles es versionado y extensible por archivo absoluto del host, con validación semántica y reinicio requerido. Las referencias futuras no crean automáticamente filas administrativas: D26 las agrega desde el catálogo. Los fixtures técnicos adicionales no se presentan como modelos oficiales.
- Money de tarifas siempre incluye moneda. Las referencias embebidas toman BillingCurrency del recurso contractual central, evitando repetir un catálogo de monedas; el archivo externo exige currency explícita y coherente. Los precios normales iniciales y caché/tramos provienen de las fuentes primarias registradas en la evidencia local; no son Batch/fast ni una factura.
- USD se habilitó en `habilitados.json` y la salida se regeneró offline; ARS sigue predeterminado y país/cultura/zona no cambian. USD pasa a estar disponible junto a ARS en los selectores del catálogo global.
- [ADR 0051](../decisions/0051-precision-tecnica-de-costos-ia.md) conserva tarifas hasta seis decimales (`numeric(18,6)`), rechazando exceso de escala/rango. Presupuestos ingresados usan MinorUnits del catálogo y AwayFromZero antes de abrir UOW, con revalidación de rango después del redondeo. Los costos/reservas y sus snapshots por llamada pertenecen a T06; una edición del catálogo reemplaza los Money del modelo y conserva referencias previamente capturadas.
- No se agregó caché para configuración/modelos/política: la revalidación consulta persistencia en cada admisión. El archivo técnico singleton requiere reinicio, documentado en operaciones.

## Pendiente de composición e Integración

El resolver interno de configuración/política está implementado, incluido resultado no configurado sin clave. El endpoint State mantiene pendiente su composición real de consultas fijas, cuotas y aviso hasta T04–T06; no se publican valores fabricados para declararlo completo. `activeTurnId` real pertenece a T05; `budgetProgress`, `dailyUsage`, reservas y snapshots de costos reales pertenecen a T06. El cálculo debe usar Money con moneda y tratar uso de caché desconocido conservadoramente.

Integración debe cargar una clave real fuera del repositorio y verificar el recorrido de proveedor, chat y cancelación después de T04–T06, además del E2E conjunto con Front. Los handlers HTTP aislados prueban traducción SDK y saneamiento; no sustituyen Anthropic real ni el recorrido Meta E8. Build/test completos con Docker corresponden al cierre conjunto, no a esta entrega focal.

## Persistencia y contrato

La migración nueva `20261010032716_AssistantConfiguration` agrega `HasKeyOverride` y amplía ProviderModelId/DisplayName de 60 a 500, igual que el contrato vigente. Up conserva datos; Down elimina el tombstone y reduce nombres, por lo que requiere revisión antes de un rollback con datos largos. Las migraciones históricas permanecen intactas. No hay nuevas tablas ni cambios RLS.

Las evidencias ejecutables, SQL, drift, hash del contrato e inventario cerrado se entregan en `.local/reviews/e8b-contract/t03-manifest.json`, `t03-files.json` y `t03-delivery.md`; el informe no reemplaza resultados ni suma repeticiones como cobertura nueva.
