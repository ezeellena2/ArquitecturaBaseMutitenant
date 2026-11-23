import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { detectar, ejecutar, listarArneses } from '../arnes.mjs';
import { git } from '../comun/git.mjs';
import { claveDelPlan, indexarSpec, nombresNuevos, parsearDiff } from './etapa.mjs';
import { cambiosDeMigracion, componenteNuevo, ERROR, esCodigoDeError, rutasDeController, rutasNuevasDeOpenApi } from './extraer.mjs';

const dirArneses = join(dirname(fileURLToPath(import.meta.url)), '..');
const temporales = [];
after(() => Promise.all(temporales.map((t) => rm(t, { recursive: true, force: true }))));

const PLAN = 'docs/plans/2026-11-30-etapa-99-prueba.md';
const SIN_COMMITS = 'docs/plans/2026-11-30-etapa-97-nada.md';
const MIGRACION = 'src/Demo.Infrastructure/Persistence/Migrations/20261130000000_Widgets.cs';

const plan = [
  '# E99 · Prueba · plan ejecutable',
  '',
  '**Estado:** pendiente · Spec: `docs/plans/formularios/99-prueba.md`.',
  '',
  '## Contrato',
  '',
  'Prefijo `/api/organization/widgets`, con `[FeatureGate("widgets")]`.',
  '',
  '| Método y ruta | Permiso |',
  '| --- | --- |',
  '| GET `/` | `widgets.read` |',
  '| GET `{id}` | `widgets.read` |',
].join('\n');

const spec = ['# E99 · Prueba (spec)', '', '## Datos', '', '**Widgets** (`IAuditable`)', '', '| Columna | Tipo |', '| --- | --- |', '| `Name` | texto |', '', 'Errores: `Prueba.Widget.NotFound`.'].join('\n');
const parte = '# E99 · Prueba: front (spec, parte)\n\nPantallas: `/org/widgets` y `/org/widgets/:id`. Las filas se pegan con `PasteGrid`.\n';
// Otra etapa nombra todo lo que la 99 inventa: no es su spec, así que no la salva.
const otra = '# E98 · Otra\n\n`tenant.Gadgets` con `Secret`, `widgets.purge`, `Prueba.Widget.Invented`, `"gizmos"`, DELETE `/api/organization/widgets/{id}`, `/org/widgets/:id/historial` y `WidgetGrid`.\n';

const migracion = `public partial class Widgets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Widgets",
            schema: "tenant",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Secret = table.Column<string>(type: "text", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Widgets", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Gadgets",
            schema: "tenant",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false)
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "Viejas", schema: "tenant", columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false) });
    }
}
`;

const contrato = (rutas) => `${JSON.stringify({ openapi: '3.1.0', paths: rutas }, null, 2)}\n`;
const permisos = (lineas) => `public static class Permissions\n{\n${lineas.map((l) => `    ${l}`).join('\n')}\n}\n`;

/** Un repo de git en una carpeta temporal, con un commit por elemento: `{ mensaje: [asunto, cuerpo…], archivos: { ruta: texto } }`. */
async function repo(commits) {
  const dir = await mkdtemp(join(tmpdir(), 'conformidad-'));
  temporales.push(dir);
  for (const a of [['init', '-q', '-b', 'main'], ['config', 'user.email', 'a@b.c'], ['config', 'user.name', 'Arnés'], ['config', 'core.autocrlf', 'false']]) await git(dir, a);
  for (const { mensaje, archivos } of commits) {
    for (const [ruta, texto] of Object.entries(archivos)) {
      await mkdir(dirname(join(dir, ruta)), { recursive: true });
      await writeFile(join(dir, ruta), texto);
    }
    await git(dir, ['add', '-A']);
    await git(dir, ['commit', '-q', ...mensaje.flatMap((m) => ['-m', m])]);
  }
  return dir;
}

async function back({ conContrato = true } = {}) {
  const base = {
    [PLAN]: plan,
    [SIN_COMMITS]: '# E97 · Nada\n\n**Estado:** pendiente · Spec: `docs/plans/formularios/97-nada.md`.\n',
    'docs/plans/formularios/99-prueba.md': spec,
    'docs/plans/formularios/99-parte-front.md': parte,
    'docs/plans/formularios/98-otra.md': otra,
    'src/Demo.Domain/Authorization/Permissions.cs': permisos(['public const string Read = "users.read";']),
  };
  const etapa = {
    [MIGRACION]: migracion,
    'src/Demo.Domain/Prueba/WidgetErrors.cs': 'public const string NotFoundCode = "Prueba.Widget.NotFound";\npublic const string InventedCode = "Prueba.Widget.Invented";\n',
    // Un renglón que solo cambia (`users.read`) no es un permiso nuevo.
    'src/Demo.Domain/Authorization/Permissions.cs': permisos(['public const string Read = "users.read"; // cambia', 'public const string WidgetsRead = "widgets.read";', 'public const string WidgetsPurge = "widgets.purge";']),
    'src/Demo.Application/Widgets/WidgetFeatureDefinitionContributor.cs': 'Definitions = [new("widgets", FeatureScope.Both, false), new("gizmos", FeatureScope.Business, false)];\n',
  };
  if (conContrato) {
    base['docs/contracts/openapi.json'] = contrato({ '/api/users': { get: {} } });
    etapa['docs/contracts/openapi.json'] = contrato({
      '/api/users': { get: {} },
      '/api/organization/widgets': { get: {} },
      '/api/organization/widgets/{id}': { get: {}, delete: {} },
    });
  } else {
    const controller = (acciones) => `[ApiController]\n[Route("api/organization/widgets")]\npublic sealed class WidgetsController : ControllerBase\n{\n${acciones.join('\n')}\n}\n`;
    base['src/Demo.Api/Controllers/WidgetsController.cs'] = controller(['    [HttpGet]', '    public IActionResult List() => Ok();']);
    etapa['src/Demo.Api/Controllers/WidgetsController.cs'] = controller(['    [HttpGet]', '    public IActionResult List() => Ok();', '    [HttpGet("{id:guid}")]', '    public IActionResult Get() => Ok();', '    [HttpPost("{id:guid}/archive")]', '    public IActionResult Archive() => Ok();']);
  }
  return repo([
    { mensaje: ['docs: base'], archivos: base },
    { mensaje: ['feat(prueba): widgets', 'Tarea: 99-prueba/B1'], archivos: etapa },
    // Sin la línea de la tarea: no es de la etapa.
    { mensaje: ['feat(otra): algo'], archivos: { 'src/Demo.Domain/Otra/OtherErrors.cs': 'public const string Code = "Otra.Cosa.Inventada";\n' } },
  ]);
}

async function front() {
  const rutas = (lineas) => `export const routes = [\n${lineas.map((l) => `  ${l},`).join('\n')}\n];\n`;
  return repo([
    { mensaje: ['docs: base'], archivos: { 'src/app/routes.tsx': rutas(['{ path: "/org/usuarios" }']) } },
    {
      mensaje: ['feat(prueba): pantallas', 'Tarea: 99-prueba/F1'],
      archivos: {
        'src/app/routes.tsx': rutas(['{ path: "/org/usuarios" }', '{ path: "/org/widgets/:widgetId" }', '{ path: "/org/widgets/:widgetId/historial" }']),
        'src/shared/ui/PasteGrid.tsx': 'export function PasteGrid() { return null; }\n',
        'src/shared/ui/WidgetGrid.tsx': 'export function WidgetGrid() { return null; }\n',
        'src/shared/ui/WidgetGrid.test.tsx': 'test("x", () => {});\n',
        'src/test/gallery.ts': 'export const caso = { path: "/org/inventada" };\n',
      },
    },
  ]);
}

async function correr(raices, planRuta = PLAN, extra = {}) {
  const arnes = (await listarArneses(dirArneses)).find((a) => a.carpeta === 'conformidad');
  const avisos = [];
  const hallazgos = await detectar({ dirArneses, arnes, raices, opciones: { plan: planRuta, ...extra }, avisos });
  return { hallazgos, avisos, nombres: hallazgos.map((h) => h.id.split('|').slice(4).join('|')) };
}

test('la clave del plan es su nombre sin la fecha ni «etapa-»', () => {
  assert.equal(claveDelPlan('docs/plans/2026-11-08-etapa-12-catalogos.md'), '12-catalogos');
  assert.equal(claveDelPlan('C:\\x\\2026-11-19-etapa-base-de-la-12-segunda-parte.md'), 'base-de-la-12-segunda-parte');
});

test('parsearDiff: archivo nuevo, números de línea de lo agregado y lo quitado', () => {
  const diff = [
    'diff --git a/src/A.cs b/src/A.cs', 'new file mode 100644', '--- /dev/null', '+++ b/src/A.cs', '@@ -0,0 +1,2 @@', '+uno', '+dos',
    'diff --git a/src/B.cs b/src/B.cs', '--- a/src/B.cs', '+++ b/src/B.cs', '@@ -3 +3 @@', '-viejo', '+nuevo', '@@ -10,0 +11 @@', '++++ raro',
  ].join('\n');
  const [a, b] = parsearDiff(diff);
  assert.deepEqual([a.ruta, a.nuevo, a.agregadas], ['src/A.cs', true, [{ n: 1, texto: 'uno' }, { n: 2, texto: 'dos' }]]);
  assert.deepEqual([b.ruta, b.nuevo, b.quitadas, b.agregadas], ['src/B.cs', false, ['viejo'], [{ n: 3, texto: 'nuevo' }, { n: 11, texto: '+++ raro' }]]);
});

test('tablas: CreateTable del Up con esquema y columnas, AddColumn, y el Down no cuenta', () => {
  const lineas = migracion.split('\n');
  const down = lineas.findIndex((l) => l.includes('void Down'));
  const agregarColumna = ['migrationBuilder.AddColumn<List<string>>(', '    name: "Tags",', '    schema: "tenant",', '    table: "Widgets",', '    type: "text[]");'];
  const conColumna = [...lineas.slice(0, down - 2), ...agregarColumna, ...lineas.slice(down - 2), ...agregarColumna.map((l) => l.replace('Tags', 'DelDown'))];
  const archivo = { agregadas: conColumna.map((texto, i) => ({ n: i + 1, texto })) };
  const { tablas, columnas } = cambiosDeMigracion(archivo);
  assert.deepEqual(tablas.map((t) => `${t.esquema}.${t.tabla}`), ['tenant.Widgets', 'tenant.Gadgets']);
  assert.deepEqual(tablas[0].columnas.map((c) => c.nombre), ['Id', 'TenantId', 'Name', 'Secret', 'CreatedAtUtc']);
  assert.equal(tablas[0].columnas[3].linea, lineas.findIndex((l) => l.includes('Secret =')) + 1);
  assert.deepEqual(columnas.map((c) => `${c.tabla}.${c.nombre}`), ['Widgets.Tags']);
});

test('spec: tablas con o sin esquema y en singular, columnas en su bloque, rutas con su método y caminos del front', () => {
  const s = indexarSpec([{ archivo: 'p.md', texto: [
    '**tenant.FormCatalogs** (`IAuditable`)', '', '| Columna | Tipo |', '| --- | --- |', '| `Code` | texto |', '',
    'La invitación suma `ConsentConfirmedAtUtc`. Un `Name` suelto no alcanza para otra tabla.', '`FormItem` es la opción.',
    'Prefijo `/api/organization/forms/catalogs`.', '| POST `/` | x |', '| GET `{id:guid}/items` | x |', '| `DELETE /api/otra/{id}` | x |',
    'Pantalla `/org/formularios/catalogos/:catalogRef`. Código `Forms.Catalog.ItemInUse` y `[FeatureGate("forms")]`.',
  ].join('\n') }]);
  assert.ok(s.tabla('FormCatalogs'));
  assert.ok(s.tabla('FormItems'), 'en singular');
  assert.ok(!s.tabla('FormCatalogItems'));
  assert.ok(s.columna('FormCatalogs', 'Code'), 'en el bloque de su tabla');
  assert.ok(s.columna('FormCatalogs', 'Version'), 'implícita');
  assert.ok(s.columna('Invitations', 'ConsentConfirmedAtUtc'), 'compuesta, entre backticks');
  assert.ok(!s.columna('Invitations', 'Name'), 'suelta, fuera del bloque de su tabla');
  assert.ok(s.rutaApi('POST', '/api/organization/forms/catalogs'));
  assert.ok(s.rutaApi('GET', '/api/organization/forms/catalogs/{id}/items'));
  assert.ok(s.rutaApi('DELETE', '/api/otra/{otraId}'));
  assert.ok(!s.rutaApi('GET', '/api/organization/forms/catalogs'), 'el método cuenta');
  assert.ok(!s.rutaApi('POST', '/api/organization/forms/catalogs/{id}/items'));
  assert.ok(s.rutaFront('/org/formularios/catalogos/:id'));
  assert.ok(!s.rutaFront('/org/formularios/catalogos'), 'el camino entero, no un pedazo');
  assert.ok(s.nombra('Forms.Catalog.ItemInUse'));
  assert.ok(!s.nombra('Forms.Catalog.Item'));
  assert.ok(s.entreComillas('forms'));
  assert.ok(!s.entreComillas('organization'));
});

test('rutas: lo que suma el contrato exportado y, sin contrato, los [Http*] con el [Route] de su clase', () => {
  const antes = contrato({ '/api/a': { get: {} } });
  const despues = contrato({ '/api/a': { get: {}, post: {} }, '/api/b/{id}': { delete: {}, parameters: [] } });
  assert.deepEqual(rutasNuevasDeOpenApi(antes, despues).map((r) => `${r.metodo} ${r.ruta}`), ['POST /api/a', 'DELETE /api/b/{id}']);
  assert.equal(rutasNuevasDeOpenApi(null, despues).length, 3);
  const texto = '[Route("api/x")]\npublic sealed class XController\n{\n[HttpGet]\n[HttpPut("{id:guid}", Name = "y")]\n[HttpPost("/api/abs")]\n}';
  const archivo = { quitadas: ['[HttpGet]'], agregadas: [{ n: 4, texto: '[HttpGet]' }, { n: 5, texto: '[HttpPut("{id:guid}", Name = "y")]' }, { n: 6, texto: '[HttpPost("/api/abs")]' }] };
  assert.deepEqual(rutasDeController(archivo, texto).map((r) => `${r.metodo} ${r.ruta}`), ['PUT /api/x/{id:guid}', 'POST /api/abs']);
});

test('componentes y nombres: solo lo que nace en shared/ui y lo que el mismo commit no quitó', () => {
  assert.equal(componenteNuevo({ nuevo: true, ruta: 'src/shared/ui/EditableTable.tsx' }), 'EditableTable');
  assert.equal(componenteNuevo({ nuevo: true, ruta: 'src/shared/ui/EditableTable.test.tsx' }), null);
  assert.equal(componenteNuevo({ nuevo: false, ruta: 'src/shared/ui/Banner.tsx' }), null);
  assert.equal(componenteNuevo({ nuevo: true, ruta: 'src/areas/x/Banner.tsx' }), null);
  const commit = (corto, agregadas, quitadas = []) => ({ corto, archivos: [{ ruta: 'src/D/XErrors.cs', quitadas, agregadas: agregadas.map((texto, i) => ({ n: i + 1, texto })) }] });
  const nuevos = nombresNuevos([commit('a', ['"A.B.Movido"'], ['"A.B.Movido"']), commit('b', ['"A.B.Nuevo"']), commit('c', ['"A.B.Nuevo"'])], esCodigoDeError, ERROR);
  assert.deepEqual(nuevos.map((n) => `${n.nombre} ${n.commit.corto}`), ['A.B.Nuevo b']);
});

test('una etapa: cada clase acierta lo que la spec nombra y marca lo que no, en los dos repos', async () => {
  const raices = { back: await back(), front: await front() };
  const { nombres, hallazgos, avisos } = await correr(raices);
  assert.deepEqual(nombres.sort(), [
    'columna:Widgets.Secret',
    'componente:WidgetGrid',
    'error:Prueba.Widget.Invented',
    'funcionalidad:gizmos',
    'permiso:widgets.purge',
    'ruta-front:/org/widgets/:widgetId/historial',
    'ruta:DELETE /api/organization/widgets/{id}',
    'tabla:tenant.Gadgets',
  ]);
  assert.ok(hallazgos.every((h) => h.accion === 'humano' && /no está en la spec \(\w+, Tarea: 99-prueba\/[BF]1\)/.test(h.real)));
  assert.equal(hallazgos.find((h) => h.tipo === 'columna').archivo, MIGRACION);
  assert.equal(hallazgos.find((h) => h.tipo === 'columna').linea, migracion.split('\n').findIndex((l) => l.includes('Secret =')) + 1);
  assert.ok(avisos.includes('back: 1 commit con «Tarea: 99-prueba/»'));
  assert.ok(avisos.some((a) => a.startsWith('back: spec: docs/plans/2026-11-30-etapa-99-prueba.md, docs/plans/formularios/99-parte-front.md, docs/plans/formularios/99-prueba.md')));
});

test('sin contrato exportado, las rutas salen de los controllers', async () => {
  const { nombres } = await correr({ back: await back({ conContrato: false }) }, PLAN, { repo: 'back' });
  assert.ok(nombres.includes('ruta:POST /api/organization/widgets/{id:guid}/archive'));
  assert.ok(!nombres.some((n) => n.startsWith('ruta:GET')));
});

test('el CLI: una línea por hallazgo, la cuenta, 0 y el aviso sin commits, y 1 sin --plan', async () => {
  const b = await back();
  const f = await front();
  const informes = await mkdtemp(join(tmpdir(), 'conformidad-informes-'));
  temporales.push(informes);
  const salida = [];
  const escribir = (t) => salida.push(t);
  assert.equal(await ejecutar(['conformidad', '--detectar', '--plan', PLAN, '--back', b, '--front', f], { dirArneses, escribir, dirInformes: informes }), 2);
  const linea = migracion.split('\n').findIndex((l) => l.includes('Secret =')) + 1;
  const columna = new RegExp(`^back · ${MIGRACION}:${linea} \\(columna\\): la columna Widgets\\.Secret no está en la spec \\(\\w+, Tarea: 99-prueba/B1\\)$`);
  assert.ok(salida.some((l) => columna.test(l)), salida.join('\n'));
  assert.equal(salida.filter((l) => / \((?:tabla|columna|error|permiso|funcionalidad|ruta|ruta-front|componente)\): /.test(l)).length, 8);
  assert.ok(salida.includes('auto: 0 · modelo: 0 · humano: 8'));

  salida.length = 0;
  assert.equal(await ejecutar(['conformidad', '--detectar', '--plan', SIN_COMMITS, '--back', b, '--front', join(b, 'no-existe')], { dirArneses, escribir, dirInformes: informes }), 0);
  assert.ok(salida.includes('back: no hay commits con «Tarea: 97-nada/»: nada que comparar'));
  assert.ok(salida.some((l) => /^back: no está el repo del front en .+: se omite$/.test(l)));
  assert.ok(salida.includes('auto: 0 · modelo: 0 · humano: 0'));

  salida.length = 0;
  assert.equal(await ejecutar(['conformidad', '--detectar', '--back', b, '--front', f], { dirArneses, escribir, dirInformes: informes }), 1);
  assert.match(salida.join('\n'), /conformidad necesita --plan <ruta del plan>/);
});
