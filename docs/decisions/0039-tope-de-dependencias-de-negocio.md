# ADR 0039 · El tope de dependencias cuenta solo las de negocio

Estado: aceptada el 2026-10-03 por el dueño del producto, a partir de la [revisión de calidad de diseño](README.md) (decisión D1).

## Contexto

`ServiceDependencyCountTests` rompe el build si una clase de `Application/Services` recibe más de 8 dependencias. Viene de `ServiceDependencyLimitTests` de ArquitecturaBase y funciona como alarma: avisa cuando una clase hace demasiadas cosas.

El problema es que también cuenta cuatro dependencias que las propias reglas obligan a inyectar en cada servicio que guarda:
- `ILogger<T>`, por `OperationLog`;
- `TimeProvider`, por la regla de fechas;
- `IRequestValidator`, porque se valida afuera;
- `IUnitOfWork`, porque es la única forma de guardar.

A un servicio que escribe le quedaban cuatro lugares para su trabajo.

Efecto medido el 2026-10-03: 46 de las 118 clases de `Application/Services` (sin contar Plataforma) tenían 7 u 8 dependencias. Para no pasarse del tope pasaron tres cosas:
- se crearon clases que solo reenvían, como `OrganizationUserLifecycleCoordinator`, `OrganizationUserExitEffects` y `SignupVerificationFlow`;
- la transacción bajó a los helpers;
- `TransactionBoundaryTests` sumó una lista blanca de 14 helpers que reciben `IUnitOfWork`, en contra de `guardado.md`.

## Decisión

- **Qué cuenta.** El tope sigue en **8**, pero solo cuentan las **dependencias de negocio**. Quedan afuera `ILogger<T>`, `TimeProvider`, `IRequestValidator` e `IUnitOfWork`. Todo lo demás cuenta, incluido `IOptions<T>`.
- **Si una clase se pasa,** se parte **por responsabilidad**, nunca con una clase que solo reenvía.
- **La transacción la abre el método público del servicio** de entrada, como en `RoleService`. Los helpers trabajan adentro y no reciben `IUnitOfWork`; la regla de `guardado.md` vuelve a valer sin excepciones. La lista blanca de `TransactionBoundaryTests` queda solo con los dos seeds.
- **Qué lo verifica:**
  - `ServiceDependencyCountTests`, con un caso de control que prueba que esas cuatro no cuentan y que una clase con 9 dependencias de negocio se pasa;
  - `TransactionBoundaryTests`.

## Alternativas descartadas

- **Contar todo y permitir excepciones con motivo, como ArquitecturaBase:** la lista tiende a crecer, como ya pasó con la de transacciones.
- **Dejar la regla igual y partir los servicios por caso de uso:** suma clases y no cambia el incentivo.
- **Sacar la alarma:** se pierde el aviso automático, y con un agente programando vuelven los servicios gigantes.

## Consecuencias

- Hoy ninguna clase pasa de 8 dependencias de negocio (el máximo es 8, en dos clases), así que el cambio no rompe nada.
- Se pueden borrar las clases que solo reenvían y devolver la transacción a los servicios:
  - lo existente se ordena en la consolidación previa a la Etapa 7;
  - lo que falta de la Etapa 5 ya nace con esta regla.
- Es una diferencia deliberada con ArquitecturaBase.
- `capas-y-flujo.md`, `guardado.md` y `backend.md` §4 tienen que decir lo mismo.
