import { posix } from 'node:path';
import { clase, documentos, lineaDeEstado, palabraDe } from '../clases.mjs';

export const nombre = 'huerfanos';

async function destinosEnlazados(ctx) {
  const enlazados = new Set();
  for (const archivo of ctx.archivos.archivos) {
    if (!archivo.endsWith('.md') || archivo.startsWith('scripts/arneses/')) continue;
    for (const e of ctx.markdown.enlaces(await ctx.leer(archivo))) {
      if (!e.ruta) continue;
      const destino = posix.normalize(posix.join(posix.dirname(archivo), e.ruta));
      if (destino !== archivo) enlazados.add(destino);
    }
  }
  return enlazados;
}

/** Un documento que nadie enlaza no lo encuentra nadie: ni una persona ni un agente. */
export async function detectar(ctx) {
  const enlazados = await destinosEnlazados(ctx);
  const out = [];
  for (const archivo of documentos(ctx)) {
    const tipo = clase(archivo);
    if (tipo === null || tipo === 'indice' || enlazados.has(archivo)) continue;
    const estado = lineaDeEstado(await ctx.leer(archivo));
    if (estado && palabraDe(estado) === 'cerrado') continue;
    out.push(ctx.crear({ archivo, linea: 1, tipo: 'huerfano', clave: 'enlaces', real: `${archivo}: ningún documento lo enlaza`, accion: 'humano' }));
  }
  return out;
}
