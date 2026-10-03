import { mkdirSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/**
 * Imita a un modelo: aplica las ediciones de un escenario (JSON apuntado por ARNES_FAKE_ESCENARIO) dentro de `cwd`
 * y devuelve el objeto `{ resultados }`. Solo lo usan los tests de los motores y de la corrida.
 */
export function aplicarEscenario(cwd) {
  const ruta = process.env.ARNES_FAKE_ESCENARIO;
  const escenario = ruta ? JSON.parse(readFileSync(ruta, 'utf8')) : { ediciones: [], resultados: [] };
  for (const e of escenario.ediciones ?? []) {
    const destino = join(cwd, e.archivo);
    if (e.crear !== undefined) {
      mkdirSync(dirname(destino), { recursive: true });
      writeFileSync(destino, e.crear);
    } else if (e.reemplazar) {
      const [de, a] = e.reemplazar;
      writeFileSync(destino, readFileSync(destino, 'utf8').replace(de, () => a));
    } else if (e.agregar !== undefined) {
      appendFileSync(destino, e.agregar);
    }
  }
  return { escenario, resultado: { resultados: escenario.resultados ?? [] } };
}

export function registrar(datos) {
  if (process.env.ARNES_FAKE_LOG) appendFileSync(process.env.ARNES_FAKE_LOG, `${JSON.stringify(datos)}\n`);
}
