# E23 · El asistente, con Casos: lo que ya se pagó allá y qué verificar (spec, parte)

**Estado:** pendiente · parte de la [spec de la E23](23-asistente.md), que dice qué leer y cómo se cierra.

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Con un índice por dueño y canal,** un operador tenía una sola operación de IA para todos sus chats: tres chats, y los dos últimos terminan.
2. **La espera de 35 s,** por un cierre que no avisaba.
3. **Un audio «Transcribiendo…» para siempre,** porque una falla del GET abortaba el POST.
4. **Una respuesta tardía** después de tomar el chat no sale.
5. **`business.information`** incluía textos sin revisar como si fueran hechos: lo vacío no se usa, y solo van los hechos confirmados.
6. **La lista de formularios se cortaba en 100 sin orden,** y el corte cambiaba entre turnos: se ordena por nombre antes de cortar.
7. **Un tema que escribió un cliente llegaba al asistente de los demás chats:** nace «Sin revisar» (219), y la lectura de los candidatos no se marca parcial por los temas.
8. **Con el tope de ofrecimientos lleno,** el formulario quedaba bloqueado sin respuesta ni derivación, aunque la persona hubiera tocado un botón del propio asistente (O36): deriva con su motivo (278).
9. **Con el tope lleno, un audio no se transcribía** y quedaba sin respuesta, aunque pidiera algo que contesta un caso (O39): se transcribe siempre (278).
10. **Un cupo diario agotado derivaba con «El asistente no pudo responder»,** sin que la persona ni el operador supieran que fue el cupo (O35): cada tope dice el suyo (271).

Y lo que vio la [prueba integral de allá](../../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase), que va también en la lista de control de la puerta:
11. **Una sugerencia hacia un formulario que publicó otra versión** lo nombra y dice qué hacer (O20).
12. **Una sugerencia armada antes de tomar la conversación y mandada después:** el servidor la rechaza con un 409 claro, y la pantalla no la muestra como un resultado incierto (O32).
13. **Un audio que el proveedor devuelve vacío** contesta o deriva, y dice por qué (270). Allá el chat decía «No pudimos pasar este audio a texto», a la persona no le llegaba nada y no se derivaba.
14. **Ante «quiero cargar el registro de lluvia y además decime a qué hora abren»,** el motor propone solo el formulario (154).

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Valores iniciales en un Flow con datos, sin endpoint. | Un perfil nuevo los admite por `data`. |
| ADR | La de respuestas redactadas (39). | Rige la [0054](../../decisions/0054-respuestas-redactadas-por-el-asistente.md): las fuentes, la forma, el validador y derivar. |
| ADR | La 0048 y la 0038. | La 0048 suma una precisión (B1), sin tocar su D8, que remite a la 0068; la 0038 se actualiza. |
| Acciones | El alcance de solo lectura y sus entradas: `IAssistantReadOnlyScope.RunAsync<TService, TResult>(work, ct)`, y `AssistantReadOnlyScope` (`Infrastructure/Modules/Assistant/`) entra con el actor que revalidó el worker (`IExecutionActorContext.Actor`, con `IExecutionActorScope.Enter`) o, sin actor, con la organización del claim de la petición (`TenantContext.AccessBinding` y `ITenantAccessInitializer.SetFromAccess`), y se niega con un `ITenantScope.Enter` técnico. | Suma la del turno de canal, con la organización y sin persona, porque `ExecutionActor` pide `OwnerUserId`. |
| Acciones | La versión del motor: `AssistantTurnWork.Create(…, offersActions)` elige `CurrentEngineVersion` (2) para un turno con acciones o `WithoutActionsEngineVersion` (1), y el worker toma `EngineVersion <= CurrentEngineVersion` (`AssistantTurnRepository`). | El turno de canal no ofrece acciones: `Create` suma la clase del turno y lo sella con 3, que pasa a ser `CurrentEngineVersion`, y el del Inicio con acciones sigue en 2 con su propia constante. |
| Costuras | `ModuleLimitResolver` e `IModuleLimitContributor` ([0069](../../decisions/0069-modulos-y-suscripciones.md) §4): cada módulo declara sus topes y el resolvedor da el de cada organización. | `assistant.maxReplyLength`, `assistant.repliesPerDay` y `assistant.formOffersPerDay` se declaran en `AssistantModuleLimitContributor` y se leen por el resolvedor (`ResolveAsync(key, organizationId, ct)`, que acepta `organizationId` nulo para una persona o Plataforma), nunca directo de la configuración de Plataforma; el valor de la organización se valida contra el tope que da. Cada máximo suma su propiedad a `PlatformAssistantSettingsResponse`, que el aporte lee con `IAssistantSettingsReader.FindPlatformAsync`, y su getter entra en `Maximums` de `ModuleLimitArchitectureTests`, como `get_DailyMessageLimit`. |
| Asistente | Cómo se separa el motor de la sesión de una persona. | Un turno de canal no necesita cuenta. |
| [E18] | El audio del chat. | Está guardado cuando se transcribe. |
| [E19] | Cómo queda el bloque en la versión sellada y en el Flow. | El prellenado lo saltea sin leerlo. |
| [E21] | La cola con su origen. | El asistente sale por la misma cola. |
| Proveedor | Quién transcribe. | OpenAI, con la [0055](../../decisions/0055-transcripcion-de-audios.md) (27). |
