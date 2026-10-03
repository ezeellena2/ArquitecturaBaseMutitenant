import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { indexarArchivos } from './archivos.mjs';
import { filtrarIgnorados, crearHallazgo, ordenar, contar } from './hallazgos.mjs';
import { anclas, enlaces, estaCerrada, marcasEtapa, rutasEnBackticks, slug } from './markdown.mjs';
import { leerEtapasCerradas } from './repos.mjs';

const fixtures = join(dirname(fileURLToPath(import.meta.url)), '..', 'realinear-docs', 'fixtures');

test('repos: lee ClosedStages de C# y de TS', async () => {
  assert.deepEqual(await leerEtapasCerradas(join(fixtures, 'back')), [0, 1, 2]);
  assert.deepEqual(await leerEtapasCerradas(join(fixtures, 'front')), [0, 1]);
  assert.deepEqual(await leerEtapasCerradas(join(fixtures, 'no-existe')), []);
});

test('archivos: indexa el árbol e ignora bin, obj y node_modules', async () => {
  const indice = await indexarArchivos(join(fixtures, 'back'));
  assert.ok(indice.existe('src/Demo.Application/Services/Roles/RoleService.cs'));
  assert.ok(indice.existe('src/Demo.Application/Services/Roles'));
  assert.ok(indice.esDirectorio('docs/features'));
  assert.deepEqual(indice.porNombre('RoleService.cs'), ['src/Demo.Application/Services/Roles/RoleService.cs']);
  assert.ok(!indice.existe('docs/features/inexistente.md'));
});

test('markdown: enlaces con ancla, sin código ni URLs', () => {
  const texto = ['# T', 'Ver [a](../x/y.md#sec-uno) y [b](https://e.com) y [c](#aqui).', '```', '[no](nada.md)', '```'].join('\n');
  assert.deepEqual(enlaces(texto), [
    { linea: 2, texto: 'a', ruta: '../x/y.md', ancla: 'sec-uno' },
    { linea: 2, texto: 'c', ruta: '', ancla: 'aqui' },
  ]);
});

test('markdown: rutas entre backticks y anclas de títulos', () => {
  const texto = ['## 1. Los cuatro niveles', 'Mirá `src/A/B.cs` y `Foo` y `docs/x.md`.'].join('\n');
  assert.deepEqual(rutasEnBackticks(texto).map((r) => r.valor), ['src/A/B.cs', 'docs/x.md']);
  assert.ok(anclas(texto).has('1-los-cuatro-niveles'));
  assert.equal(slug('Mapa de carpetas → punteros'), 'mapa-de-carpetas--punteros');
});

test('markdown: marcas de etapa y rangos cerrados', () => {
  assert.deepEqual(marcasEtapa('algo (E4) y [E5]'), [[4, 4], [5, 5]]);
  assert.deepEqual(marcasEtapa('nace en E3–E5'), [[3, 5]]);
  assert.equal(estaCerrada([[3, 5]], [0, 3, 4]), false);
  assert.equal(estaCerrada([[3, 4]], [0, 3, 4]), true);
  assert.equal(estaCerrada([], [0]), true);
});

test('hallazgos: id estable, orden y filtro por ignorados', () => {
  const h = crearHallazgo({ detector: 'referencias', repo: 'back', archivo: 'a.md', linea: 3, tipo: 'ruta-inexistente', clave: 'x/y.cs', real: 'no existe', accion: 'humano' });
  assert.equal(h.id, 'referencias|back|a.md|ruta-inexistente|x/y.cs');
  const otro = crearHallazgo({ detector: 'referencias', repo: 'back', archivo: 'a.md', linea: 1, tipo: 'ruta-inexistente', clave: 'z', real: 'r', accion: 'auto' });
  assert.deepEqual(ordenar([h, otro]).map((x) => x.linea), [1, 3]);
  assert.deepEqual(filtrarIgnorados([h, otro], [{ id: h.id, motivo: 'a propósito', fecha: '2026-10-02' }]), [otro]);
  assert.deepEqual(contar([h, otro]), { auto: 1, modelo: 0, humano: 1 });
});
