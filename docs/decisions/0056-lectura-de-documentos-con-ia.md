# ADR 0056 · Lectura de documentos con IA

**Estado:** aceptada el 2026-11-08, con el [plan de la E25](../plans/2026-11-08-etapa-25-documentos.md); se implementa en esa etapa. Toma la 0022 de ArquitecturaBase («Lectura de documentos con IA»), adaptada a este repo.

## Contexto

Una foto, un PDF o un archivo de Office que llega por WhatsApp hoy queda como archivo del chat (E18). La E25 lo lee con IA y propone el formulario que corresponde, prellenado (decisiones 70, 71, 99 y 199). Esos documentos traen datos financieros y personales, como un CBU, un CUIT, montos y nombres, y leerlos implica mandarlos a un proveedor externo. Ningún tipo de documento puede quedar escrito en el código.

## Decisión

1. **Qué se lee.** Lo que manda un empleado activo, con teléfono verificado y `formrecords.create`, en Internos, y lo que manda cualquier contacto de Clientes (49 y 70), siempre para los formularios que son para esa persona (50). Se leen fotos JPEG y PNG, PDF de hasta 10 páginas, `.docx` y `.xlsx`; la ubicación y el contacto se usan sin IA. No se leen `.doc`, `.xls`, los archivos con macros, los que tienen contraseña ni un CSV por WhatsApp. El tipo sale del contenido (`IFileContentInspector`, E18), nunca de la extensión.
2. **Antes de la IA.** Word y Excel se pasan a texto y tablas en el servidor, sin ejecutar nada, con los topes de las planillas de la E12. Las bibliotecas son las que ya están: `DocumentFormat.OpenXml` (con ClosedXML) y PDFsharp.
3. **Al proveedor va solo el documento preparado y las preguntas candidatas.** Nada más de la persona ni de otras respuestas. Se registra el hash y el tamaño, nunca el contenido. Lee el modelo calificado de `Assistant:Documents:ProviderModelId`, dentro del tope mensual de IA de la organización (0051), con un tope por persona y por día (`Forms:DocumentReadsPerPersonPerDay`). Al llegar a un tope, el documento queda en la bandeja sin leer.
4. **Lo leído es un dato, nunca una instrucción.** El modelo clasifica y propone; Forms valida y escribe. Un texto escondido en el documento no cambia lo que hace el sistema.
5. **Validaciones fijas en el servidor, sin IA.** Un conjunto cerrado en el código: CBU y CVU con sus dígitos verificadores, CUIT y CUIL con el validador de `platform.TaxIdTypes`, la entidad que sale de la cuenta (la tabla del BCRA, `platform.BankEntities`, un dato de referencia con su seed), el monto y la fecha. Más los duplicados: el mismo archivo, o la misma operación, monto y fecha. Un dato que no las pasa no se propone como válido, y todas se repiten al confirmar.
6. **Confirma una persona.** «Cargar solo si es seguro» carga sola, por formulario, únicamente con las tres señales de la decisión 71: reconocido sin duda, validaciones bien y sin duplicado, y las últimas 20 cargas confirmadas sin correcciones. Nunca se usa la seguridad que declara el modelo. Un documento que se reconoce como un formulario solo para la app no se ofrece por WhatsApp: queda en «Documentos recibidos», para pasarlo al formulario desde la web (203).
7. **Un Flow no admite un archivo precargado.** La documentación de Meta dice que `PhotoPicker` y `DocumentPicker` no se inicializan con `init-values`. Con el perfil `flow-static-7.3-r14`, el selector que recibe el documento se oculta con `visible` por un dato del envío, y en su lugar se ve «Adjuntamos tu documento», sin «Cambiar documento». La captura toma la copia guardada. El `SourceKey` es `document:<id>` por el Flow, por «Pasar al formulario» y por «Cargar solo si es seguro», así un documento no se carga dos veces.
8. **Retención.** El documento usado sigue a su respuesta (23). El descartado o sin clasificar se borra a los 90 días, con su lectura (41). La copia se confirma después del commit, y `FormDocuments` está en la consulta de lo que se usa, así la limpieza de la E18 no la borra.
9. **Genérico.** Ningún tipo de documento va en el código. Un estándar nuevo es una plantilla de la biblioteca (40), con su «Lectura de documentos».

## Consecuencias

- El asistente suma el propósito «leer documento», con su perfil calificado.
- Forms suma la bandeja «Documentos recibidos», la «Lectura de documentos» de un formulario y de una plantilla, y la precisión por formulario y por dato.
- WhatsApp entrega el documento a Forms por un puerto neutral (`IStructuredDocumentSink`). Forms pregunta por otro (`IStructuredInboxAccess`) si quien revisa atiende la bandeja de donde llegó.
- Queda el riesgo de un dato bien formado pero mal leído, como un monto en una foto. Lo acotan las validaciones fijas, la confirmación y el registro de correcciones, que se suman como ejemplos.

## Alternativas descartadas

- **Un lector entrenado por diseño fijo:** pide ejemplos marcados por cada tipo de documento. Puede volver más adelante como opción para un diseño con mucho volumen.
- **Cargar sin confirmar desde el principio:** sin precisión medida no hay forma de saber cuándo es seguro.
- **Leer lo que manda un número que no es contacto:** queda fuera de la etapa.
- **Precargar el archivo en el Flow, con «Cambiar documento»:** Meta no lo admite.

## Verificación

Antes de habilitar el perfil, se califica con casos ficticios: comprobantes de varios bancos, fotos movidas, documentos que no son de ningún tipo, instrucciones escondidas, Word y Excel. Los tests de la E25 prueban lo que se manda al proveedor, cada validación fija, los duplicados, las tres señales, la retención a los 90 días y que la copia sigue a las 24 horas. La prueba real con Meta queda para la I1 de la 10.
