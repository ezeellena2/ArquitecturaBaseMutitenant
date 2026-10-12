# ADR 0046 · Nombre y apellido en dos campos, y el nombre de Google

Estado: aceptada el 2026-10-06 por el dueño del producto.

## Contexto

Mi cuenta pide «Nombre y apellido» en un solo campo (`DisplayName`). Además, quien ingresa o se registra con Google queda sin nombre, aunque Google lo trae. El dueño del producto lo pidió al probar: «nombre y apellido deberíamos dividirlo en 2 inputs distintos, y si me logueo con Google ya me debería traer el nombre y apellido que tiene Google».

## Decisión

- **La identidad guarda el nombre y el apellido por separado.** Los dos son opcionales y tienen su tope de largo en `TextLimits`.
- **El nombre que se muestra** (listas, menús, auditoría, correos) se arma con los dos, en un solo lugar. Ninguna pantalla los junta por su cuenta.
- **Mi cuenta** tiene dos campos, «Nombre» y «Apellido», como lo dibuje Diseño.
- **Google** completa el nombre y el apellido (`given_name` y `family_name`) al registrarse, al ingresar y al vincularlo, **solo si están vacíos**. Nunca pisa lo que la persona escribió.
- **Los datos que ya existen** se migran: el valor actual se parte en nombre y apellido, con la primera palabra como nombre y el resto como apellido. La persona lo puede corregir.

## Consecuencias

- **Back:**
  - una migración, con la partición de los datos existentes;
  - los contratos de `/api/me` y de las lecturas que muestran nombres;
  - la semilla del operador: `Seed:PlatformOwner:DisplayName` pasa a nombre y apellido, sin romper la clave anterior;
  - la invitación y el registro, si piden nombre;
  - los tests.
- **Front:** Mi cuenta con dos campos, y los tipos regenerados.
- **Lienzo:** Diseño dibuja «Tus datos» de Cuenta y M-Cuenta con los dos campos, y cualquier otro tablero que edite el nombre.
- **Plan:** A6 y A7 de los [arreglos antes de la Etapa 7](../architecture/backend.md).
