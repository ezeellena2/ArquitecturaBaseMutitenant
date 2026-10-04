# Revisión de decisiones contra el código y los docs

**Fecha:** 2026-10-03.

**Alcance:** 406 decisiones de cuatro fuentes. Algunas aparecen en más de una.
- las 41 ADR y la decisión de operadores;
- las secciones «Decisiones tomadas» de 16 planes e informes;
- las 120 filas del tablero «Criterio · Decisiones por pantalla» del lienzo;
- las decisiones numeradas del usuario en la alineación y en la Etapa 5.

**Cómo:** cinco revisores, solo lectura, sobre el código en disco. Cada uno contestó tres preguntas por decisión: ¿los docs dicen lo mismo?, ¿el código la cumple?, ¿hay un test que la vigile? El detalle con archivo y línea está en [`2026-10-03-revision-de-decisiones/`](2026-10-03-revision-de-decisiones/).

## Resultado

| Veredicto | Cantidad | Qué significa |
|---|---|---|
| ✅ alineada | 226 | Los docs, el código y el test dicen lo mismo |
| 🕒 pendiente por plan | 116 | Es de una etapa futura, de la consolidación o de un paquete, y ahí está anotada. Casi todas son pantallas del lienzo que todavía no se programaron |
| 📄 docs viejos | 30 | El código cumple, pero algún doc dice otra cosa |
| 🔁 reemplazada | 19 | Una decisión posterior la cambió. La mayoría es historia de etapas cerradas y no hay que tocarla. Las que siguen escritas como vigentes van abajo |
| ❌ el código no la cumple | 7 | Abajo, con su destino |
| ⚠️ contradice otra decisión | 8 | Abajo, con su resolución o su pregunta |

**Lectura:** el proyecto está alineado en lo importante. No apareció ninguna decisión de seguridad ni de aislamiento rota. Lo que falla son docs que quedaron atrás de decisiones nuevas y unos pocos casos de código, todos con destino.

## ❌ Código que no cumple una decisión

1. **Accesibilidad (ADR 0029):**
   - axe no corre en Roles, Editar rol, Empresas, Ficha de empresa, Registrá tu empresa ni Aceptar términos;
   - ningún test lo exige, y la ficha de accesibilidad manda copiar `RolesPage.test.tsx`, que tampoco lo corre.
   - **Destino:** consolidación, bloques 5 y 6. Agregar axe en esas pantallas y un chequeo en el arnés del front de que todo `pages/*Page.test.tsx` lo corra.
2. **Contrato front ↔ back en el CI (ADR 0016):** el CI del front no lo compara, porque descarga un solo repo. Localmente sí se controla. **Destino:** pregunta 4.
3. **Archivos de ADR:** el índice pide uno por decisión implementada, y no lo tienen de la 0008 a la 0032, la 0034, la 0035 ni la 0036. **Destino:** pregunta 4.
4. **Buscar una cuenta en Plataforma:** busca por la copia `ApplicationUser.Email`, contra `emails.md`. **Destino:** back de la Etapa 5.
5. **Un operador nunca entra como persona ni a una organización** (decisión 17 de la Etapa 5): hoy el código no lo impide. **Destino:** back de la Etapa 5, en T16, con sus tests.
6. **Correos de ventas y soporte** (decisión 6): todavía no están en la configuración de la plataforma, y el front tiene fijo «soporte@ejemplo.com». **Destino:** ya está en curso en el back de la Etapa 5, como ampliación de T05.
7. **Del lienzo:**
   - la línea fina entre las opciones del menú lateral (R19): va al Paquete 2;
   - la portada todavía tiene la etiqueta del héroe y el menú en otro orden (P92): va a la Etapa 7.

## ⚠️ Contradicciones y cómo se resuelven

| # | Contradicción | Resolución |
|---|---|---|
| 1 | La ADR 0034 (y la 0012) dice que hay tres alcances, pero la decisión 3 sacó «Cada empresa» de la pantalla, y lo dejó en la API para el rol de sistema | Ya está decidido, solo falta escribirlo. Se enmiendan la 0012 y la 0034, `backend.md` y `multitenancy.md`, y en el `arbol.md` del front se saca la opción de `RoleScopeField`. En la consolidación (bloque 6), el editor deja de crear roles con `?scope=AnyCompany` |
| 2 | ADR 0038: «Cargar teléfonos de empleados» le mostraría a la organización qué números tienen cuenta, contra la ADR 0041 | Por la 0041 («en todos lados»), muestra «ya es miembro» o «se invita por WhatsApp», y la cuenta se vincula al aceptar. Se enmiendan la 0038 y el plan de la Etapa 11, y Diseño ajusta el tablero |
| 3 | Una baja la bloquean «las organizaciones no cerradas» (ADR 0035 y baja por Plataforma) o «solo las activas» (la baja de la persona) | **Pregunta 1** |
| 4 | Empresa fija el CUIT argentino, contra `identificacion-fiscal.md` («no suponer que todos son argentinos») | **Pregunta 2** |
| 5 | Existe la carpeta `tools/` (el preparador del E2E real), contra la ADR 0017 | **Pregunta 4** |
| 6 | El Api tiene respuestas propias, contra la ADR 0002 («las respuestas son los `*Response` de Application») | **Pregunta 4** |
| 7 | Lienzo: ¿una ficha lleva pestañas aunque una tenga un solo dato? (R21 contra Criterio-Marco) | Diseño, con el usuario, antes de las fichas de Plataforma |
| 8 | Lienzo: al editar un rol en el teléfono, ¿«Cancelar» o «Descartar cambios»? (P63 contra R20) | Diseño, con el usuario |

## Preguntas para el usuario

**Decididas el 2026-10-03: las cuatro con la opción recomendada.**
- La 1 quedó en la ADR 0035 y en D5 de la consolidación.
- La 2 quedó en D6.
- La 3 quedó en D7.
- La 4 quedó en las enmiendas del índice de ADR, en las ADR 0002, 0016 y 0017, y en la Etapa 10.

La decisión de operadores es la ADR 0042.

1. **Única Dueña y baja.** Si la cuenta es la única Dueña de una organización suspendida, ¿puede darse de baja? *Recomendado:* no. Bloquean todas las organizaciones no cerradas, como dicen la ADR 0035 y la baja por Plataforma. Si no, al reactivarla nadie podría administrarla.
2. **El tipo de identificación fiscal en Empresa.** *Recomendado:* el formulario sigue pidiendo solo el número, como en la decisión 5, y el tipo sale del país de la organización (CUIT en Argentina). Así la plantilla sirve para otro país.
3. **Auditoría y ex miembros.** Hoy muestra el correo actual de quien actuó, aunque ya no sea miembro. *Recomendado:* para un ex miembro, solo el nombre.
4. **Cuatro enmiendas técnicas.** *Recomendado:* las cuatro como siguen.
   - ADR 0017: `tools/` se admite solo para el soporte del E2E real, nunca para operar.
   - ADR 0016: el chequeo cruzado en el CI va a la Etapa 10, y los docs que dicen que ya corre se corrigen.
   - ADR 0002: se admiten `*HttpResponse` en `Api/Contracts` cuando la respuesta lleva value objects o es de protocolo, con un test de salida.
   - Índice de ADR: una decisión de una línea puede vivir solo en la tabla, y la 0035 recibe su archivo.

## Reparto

**Diseño** (lo resuelve con el usuario, como siempre):
- las contradicciones 7 y 8, y si «Tus datos» de Mi cuenta puede ir en tres columnas (R1);
- tres filas viejas del tablero de decisiones: P10, P44 y P72;
- reglas que faltan en los Criterio-*:
  - los avisos en el teléfono (R3);
  - no revelar cuentas (R4);
  - «Para resolver» (R18);
  - el detalle en grilla (R22);
  - la excepción del ☰ en el sitio público (R14);
- «Cargar teléfonos de empleados» según la ADR 0041.

**Back de la Etapa 5:**
- **T05:** además de guardar los correos de ventas y soporte, una lectura pública para que los usen Ingreso y Registro.
- **T11:** la búsqueda de cuentas no usa la copia `ApplicationUser.Email`.
- **T16:** un operador nunca entra como persona ni a una organización. Hay que controlarlo en `ConnectService`, en la elección del acceso y en las invitaciones, con tests.

**Consolidación:**
- **Bloque 0 (docs):** los 📄 de los anexos y las enmiendas de este informe. Los principales:
  - `backend.md`: §16 con el `FeatureGateMiddleware`, rutas `api/organization/*`, la excepción de Plataforma a «la búsqueda global vive solo en Identity», los alcances de roles y §15 de WhatsApp;
  - `multitenancy.md`: los alcances y el operador exclusivo en §11;
  - `arbol.md` de los dos repos;
  - en `docs/rules`: `api-http.md`, `modulos.md` (`IPendingItemSource` y los avisos nuevos), `telefonos.md` (el parser ya existe) y `datos-personales.md` (la regla de la 0041 con su «Lo verifica»);
  - las fichas `identidad.md`, `roles.md` e `inicio.md`;
  - en el front: `frontend.md`, `responsive.md` y `formularios.md`, con las reglas que el lienzo cambió;
  - el plan maestro: `HarnessStage`, el ☰ de la página pública y la versión del lienzo de la Etapa 11;
  - `AGENTS.md`: el enlace a `agregar-un-area.md` y el operador.
- **Código:**
  - axe (❌1);
  - sacar `?scope=AnyCompany` del editor de roles;
  - `InvitationAcceptanceCredentials` es otra clase que solo reenvía (bloque 3);
  - D4 apunta al error del correo (`Identity.LoginMethod.AlreadyUsed`), no al de Google; vincular Google queda afuera de la 0041, porque exige probar con OAuth que la cuenta es tuya;
  - B4 suma la fila por empresa de Inicio-Miembro;
  - la 1.3 pasa `PublicPage.ContactPhone` al value object `PhoneNumber`;
  - borrar las cuatro carpetas sobrantes de `tools/` (solo `bin` y `obj`);
  - lo que salga de las preguntas 1 a 3.

**Etapas futuras:**
- **Etapa 7:** la portada (P92).
- **Etapa 8:** decidir si los nombres de WhatsApp que quedaron en el núcleo pasan al módulo.
- **Etapa 10:** el chequeo cruzado de contratos en el CI, si se aprueba la pregunta 4.
- **Etapa 11:**
  - la fuente de «Para resolver» para «Falta medio de pago»;
  - «Cargar teléfonos» según la 0041;
  - el alta de Meta (Diseño la está dibujando);
  - el panel propio del módulo.

## Para que no se vuelva a desalinear

La puerta de cierre de cada etapa suma dos controles, sin herramientas nuevas:
- cada decisión nueva tiene que quedar en su ADR o en «Decisiones tomadas», en su ficha y, si es una conducta, con su test;
- `realinear-docs` tiene que quedar en cero. Hoy lo dice `arnes.md`, pero no la puerta del plan maestro.

La decisión de operadores (permisos individuales, una cuenta exclusiva de Plataforma, el último administrador protegido) recibe su ADR, la 0042, porque cambia el modelo de accesos.
