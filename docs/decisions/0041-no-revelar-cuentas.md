# ADR 0041 · Nada revela si un correo o un teléfono tiene cuenta

Estado: aceptada el 2026-10-03 por el dueño del producto. Primero salió para «Recuperar mi cuenta» (lienzo v82, tablero «Criterio · Decisiones por pantalla»). Después el dueño la extendió a Mi cuenta.

## Contexto

En esta plantilla cualquiera puede crearse una cuenta en un minuto, con un código por correo. Por eso, una pantalla que responde «ese correo ya tiene cuenta» permite averiguar quién está registrado probando correos o teléfonos, con o sin sesión. Ingreso y Registro ya respondían igual exista o no la cuenta. Pero Recuperar y Mi cuenta (agregar un método) avisaban «ya lo usa otra cuenta».

## Decisión

Ninguna pantalla, respuesta de la API ni mensaje a quien hace el pedido revela si un correo o un teléfono es de una cuenta:

- **Ingreso y registro:** la pantalla es la misma. El código sirve para entrar si la cuenta existe.
- **Recuperar mi cuenta:** si el correo o el número nuevo ya es de otra cuenta, la pantalla sigue igual («Te mandamos un código a…»). No se manda código, y a ese correo o número le llega el aviso «Ya tenés una cuenta con este correo» (plantilla `recuperacion_metodo_en_uso` por WhatsApp).
- **Mi cuenta, agregar un correo o un WhatsApp:** si es de otra cuenta, la pantalla sigue igual, no se manda código y al dueño le llega un aviso. Desaparecen «Este número ya está asociado a una cuenta.» y su versión para el correo.
- **La respuesta es la misma en todos los casos:** mismo código HTTP, mismo cuerpo y la misma espera para reenviar. Los límites por destino y por cuenta se aplican igual.

**Excepción:** un operador de plataforma con permiso sobre las cuentas ya las ve en Plataforma › Cuentas. Por eso, al invitar a un operador, sigue valiendo el rechazo de un correo que ya tiene cuenta (decisión de operadores del 2026-10-03).

## Alternativas descartadas

- Revelarlo solo con la sesión iniciada: no protege nada, porque crear una cuenta para preguntar es inmediato.

## Consecuencias

- **Recuperar:** lo implementa el back de la Etapa 5 (T14).
- **Mi cuenta:**
  - el correo, que ya está programado, se corrige en la consolidación previa a la Etapa 7;
  - el WhatsApp nace así en la Etapa 8.
- **Lienzo:** Diseño ajusta Cuenta-Agregar y M-Cuenta-Agregar y suma en Mensajes el aviso para quien ya tenía ese correo o número.
- **Tests:** un caso con el correo libre y otro con el correo de otra cuenta tienen que dar la misma respuesta. En el segundo, el aviso se encola al dueño y no se emite ningún código.
