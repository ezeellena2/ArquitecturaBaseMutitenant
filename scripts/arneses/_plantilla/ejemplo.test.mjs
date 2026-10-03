import assert from 'node:assert/strict';
import { test } from 'node:test';
import { detectar } from './detectores/ejemplo.mjs';

test('ejemplo: marca el archivo con TODO-ARNES', async () => {
  const ctx = {
    archivos: { archivos: ['docs/a.md', 'docs/b.md'] },
    excluido: () => false,
    leer: async (rel) => (rel === 'docs/a.md' ? 'hola TODO-ARNES' : 'chau'),
    crear: (campos) => ({ ...campos }),
  };
  const hallazgos = await detectar(ctx);
  assert.equal(hallazgos.length, 1);
  assert.equal(hallazgos[0].archivo, 'docs/a.md');
});
