import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { CONTRATO, etapaDe, hallazgo, leerEn } from '../etapa.mjs';
import { esController, rutasDeController, rutasNuevasDeOpenApi } from '../extraer.mjs';

export const nombre = 'rutas';
export const repos = ['back'];

/**
 * Cada operación HTTP nueva está en una fila de la spec con su método. Sale del contrato exportado
 * (`docs/contracts/openapi.json`, antes y después de cada commit que lo toca); sin contrato, de los `[Http*]` de los controllers.
 */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  const conContrato = existsSync(join(ctx.raiz, CONTRATO));
  const out = new Map();
  for (const commit of etapa.commits) {
    const nuevas = [];
    if (conContrato) {
      if (!commit.archivos.some((a) => a.ruta === CONTRATO)) continue;
      const antes = await leerEn(ctx.raiz, `${commit.sha}^`, CONTRATO);
      const despues = await leerEn(ctx.raiz, commit.sha, CONTRATO);
      nuevas.push(...rutasNuevasDeOpenApi(antes, despues).map((r) => ({ ...r, archivo: CONTRATO })));
    } else {
      for (const archivo of commit.archivos.filter((a) => esController(a.ruta))) {
        const texto = await leerEn(ctx.raiz, commit.sha, archivo.ruta);
        nuevas.push(...rutasDeController(archivo, texto).map((r) => ({ ...r, archivo: archivo.ruta })));
      }
    }
    for (const r of nuevas) {
      const operacion = `${r.metodo} ${r.ruta}`;
      if (out.has(operacion) || etapa.spec.rutaApi(r.metodo, r.ruta)) continue;
      out.set(operacion, hallazgo(ctx, etapa, { tipo: 'ruta', nombre: operacion, commit, archivo: r.archivo, linea: r.linea }));
    }
  }
  return [...out.values()];
}
