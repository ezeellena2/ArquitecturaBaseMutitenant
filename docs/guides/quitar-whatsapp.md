# Quitar WhatsApp de una instalación

Esta receta se ejecuta en una **copia descartable** del back y con bases propias. No se borra el módulo ni se cambia el modelo de los repos compartidos para probarla. El canal pertenece al número de plataforma de E8; atención de organizaciones es E11 y el asistente es E8b.

Antes de retirarlo de una instalación con usuarios, cada cuenta cuyo único método disponible sea Phone necesita incorporar y verificar un método alternativo. Apagar el canal conserva los métodos guardados, pero no permite entrar con ellos ([ADR 0033](../decisions/0033-metodos-de-ingreso.md)). Email, Google, Phone, LoginLink y los puertos neutrales pertenecen al núcleo.

## Preparar la copia

Copiá el checkout con sus migraciones y registrá el commit de origen. Usá dos bases PostgreSQL descartables: una con toda la historia aplicada y otra vacía. No copies secretos, conexiones productivas ni directorios de datos. Conservá temporalmente `Domain/WhatsApp` y las tres carpetas `Modules/WhatsApp`: EF todavía necesita comparar el snapshot anterior con el modelo nuevo.

En la copia, retirás la sentencia de composición WhatsApp de `Program.cs` y excluís temporalmente `WhatsAppModelContributor` del descubrimiento neutral de `PersistenceModelContributors`. La exclusión debe aplicar también a DesignTime y DatabaseBootstrap; quitar solo el registro DI no alcanza, porque esas vías descubren contribuidores sin DI. Es una edición temporal de la copia, nunca una opción productiva ni una referencia permanente del núcleo al módulo.

Construí el modelo de las tres vías y comprobá que ya no incluye las entidades aportadas por WhatsApp, mientras conserva todas las del núcleo y de otros módulos. El interruptor `PhoneNumberId` no sirve para generar este modelo: apagarlo conserva el esquema.

## Generar y revisar la migración de retiro

Seguí [migración](migracion.md) con nombre `DropWhatsApp`:

```text
dotnet ef migrations add DropWhatsApp --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations -- --environment Development --Logging:EventLog:LogLevel:Default None
```

Revisá `Up`, `Down`, el snapshot y el SQL. Solo pueden desaparecer tablas y relaciones aportadas por el contribuidor WhatsApp. **No se borran `identity.LoginMethods`, `identity.LoginLinks`, cuentas, outbox, anillo Data Protection ni tablas de otros módulos.** La guía no autoriza ejecutar un Drop en una base compartida.

Conservá las migraciones históricas y sus `.Designer.cs` sin reescribirlas: la base vacía debe poder recorrer también la creación anterior del módulo y su retiro. Los modelos históricos describen las entidades por nombre; una referencia CLR histórica al módulo impediría borrar sus fuentes y debe corregirse mediante el diseño de la migración antes de aceptar el módulo, no alterando historia aplicada.

## Retirar fuentes y probar

Después de generar la migración nueva, quitá de la copia `Domain/WhatsApp`, `Application/Modules/WhatsApp`, `Infrastructure/Modules/WhatsApp`, `Api/Modules/WhatsApp`, tests exclusivos, opciones y assets propios. Conservá los puertos neutrales, Phone y LoginLink. Quitá también la exclusión temporal del descubrimiento: el contribuidor ya no existe y el modelo debe seguir coincidiendo con el snapshot de retiro.

La fixture privada E8 es también exclusiva del módulo: en la copia quitá `TestFeatures/WhatsAppBrowser`, sus `WhatsAppBrowserFixtureTests`/`WhatsAppBrowserControlTests`, el modo `--whatsapp-browser` de `RealE2ESetup` y la referencia del tool al proyecto de integración que incorporó ese modo. Conservá el setup de correo/Google y sus otros comandos. No retires el soporte neutral de `ApiFactory`, OIDC, Phone o outbox. El runner del Front de WhatsApp no se ejecuta en esa copia; los recorridos del núcleo sí.

Ejecutá build, suites del núcleo y `has-pending-model-changes` según la guía de migraciones. Aplicá el retiro en la base que ya estaba migrada; en la vacía aplicá toda la historia, incluida `DropWhatsApp`. En ambas verificá Email, Google, registro personal/empresarial, legales, recuperación y gestión de métodos; cero capacidades/canales/rutas WhatsApp y disponibilidad falsa. Comprobá también que la presencia de otro contribuidor no aplica dos veces las configuraciones ni borra sus tablas.

Registrá commits de origen, bases descartables, migración, SQL revisado y resultados en el informe de Integración. La receta documentada y un modelo de test sin módulo no sustituyen esta prueba de retiro completa.
