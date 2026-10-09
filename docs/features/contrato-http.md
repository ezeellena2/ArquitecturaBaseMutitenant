# Contrato HTTP y errores técnicos

El contrato HTTP es transversal: no representa un módulo de negocio ni una capacidad de la cuenta. Los controllers y exportadores siguen [API HTTP](../rules/api-http.md) y [Result y errores](../rules/result-y-errores.md).

`General.Contract.NotImplemented` es el estado temporal definido por `ContractErrors` para publicar un contrato antes de implementar una ruta. No es un error de identidad ni de WhatsApp. Después de implementar la ruta se retira su respuesta 501 y su descripción temporal del exportado; en E8 este retiro fue coordinado entre Back y Front. Ninguna ruta implementada de E8 lo devuelve.

`General.Unexpected` representa una falla técnica no prevista y `General.ConcurrencyConflict`, una escritura sobre una versión vencida. Su traducción y el mapper de ProblemDetails pertenecen al núcleo; no habilitan operaciones ni reemplazan los errores funcionales del área. Los errores HTTP mantienen `code` y `traceId` y nunca incluyen códigos de ingreso, tokens o datos secretos.

La igualdad del exportado la comprueban `OpenApiContractTests` (E1); las formas de contacto, enlaces consumer, errores bilingües y ausencia de metadata temporal en E8, `Etapa8ContractTests` (E8).
