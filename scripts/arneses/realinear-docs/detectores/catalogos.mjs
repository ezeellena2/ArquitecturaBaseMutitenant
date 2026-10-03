import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { contextoDe } from '../../comun/hallazgos.mjs';
import { lineas, lineasPlaneadas } from '../../comun/markdown.mjs';
import { parecidos } from '../../comun/similitud.mjs';

export const nombre = 'catalogos';
export const repos = ['back'];

const metodos = 'GET|POST|PUT|PATCH|DELETE';
const reRuta = new RegExp(`^((?:${metodos})(?:/(?:${metodos}))*)?\\s*(/api/[^\\s?]*)(?:\\?.*)?$`);
const extensionesDeArchivo = new Set(['md', 'cs', 'ts', 'tsx', 'json', 'mjs', 'resx', 'yml', 'ps1', 'html', 'txt', 'csproj']);

const normalizar = (ruta) => ruta.replace(/\{[^}]*\}/g, '{}').replace(/\/$/, '');

async function leerCatalogos(ctx) {
  const permisos = new Set();
  const codigos = new Set();
  for (const archivo of ctx.archivos.archivos) {
    if (!archivo.endsWith('.cs') || !archivo.startsWith('src/')) continue;
    const esPermisos = /\/Authorization\/\w*Permissions\.cs$/.test(archivo);
    const esErrores = /Errors\.cs$/.test(archivo);
    if (!esPermisos && !esErrores) continue;
    const texto = await ctx.leer(archivo);
    if (esPermisos) for (const m of texto.matchAll(/=\s*"([a-z][a-z0-9]*(?:\.[a-z0-9-]+)+)"/g)) permisos.add(m[1]);
    if (esErrores) for (const m of texto.matchAll(/"([A-Z]\w*(?:\.[A-Z]\w*){2,})"/g)) codigos.add(m[1]);
  }
  const rutas = new Map();
  try {
    const openapi = JSON.parse(await readFile(join(ctx.raiz, 'docs', 'contracts', 'openapi.json'), 'utf8'));
    for (const [ruta, ops] of Object.entries(openapi.paths ?? {})) {
      rutas.set(normalizar(ruta), { ruta, metodos: new Set(Object.keys(ops).map((m) => m.toUpperCase())) });
    }
  } catch {
    /* sin openapi.json no hay rutas que comparar */
  }
  return { permisos, codigos, rutas };
}

const primero = (clave, sep = '.') => clave.split(sep)[0];

export async function detectar(ctx) {
  const { permisos, codigos, rutas } = await leerCatalogos(ctx);
  const gruposPermisos = new Set([...permisos].map((p) => primero(p)));
  const gruposCodigos = new Set([...codigos].map((c) => primero(c)));
  const gruposRutas = new Set([...rutas.keys()].map((r) => r.split('/')[2]).filter(Boolean));
  const documentados = { permisos: new Set(), codigos: new Set(), rutas: new Set() };
  const out = [];

  const docs = ctx.archivos.archivos.filter((a) => /^docs\/features\/[^/]+\.md$/.test(a) && !ctx.excluido(a));
  for (const archivo of docs) {
    const texto = await ctx.leer(archivo);
    const planeadas = lineasPlaneadas(texto, ctx.etapasCerradas);
    for (const l of lineas(texto)) {
      if (l.codigo) continue;
      for (const m of l.contenido.matchAll(/`([^`\n]+)`/g)) {
        const valor = m[1].trim();
        const aqui = { archivo, linea: l.n, contexto: contextoDe(texto, l.n), accion: 'modelo' };
        const planeada = planeadas.has(l.n);

        const ruta = valor.match(reRuta);
        if (ruta) {
          const grupo = ruta[2].split('/')[2];
          if (grupo) documentados.rutas.add(grupo);
          if (planeada || !rutas.size) continue;
          const real = rutas.get(normalizar(ruta[2]));
          const pedidos = ruta[1]?.split('/') ?? [];
          if (!real) {
            const cercanas = parecidos(normalizar(ruta[2]), [...rutas.keys()], { tolerancia: 6 }).map((c) => rutas.get(c.nombre).ruta);
            out.push(ctx.crear({ ...aqui, tipo: 'ruta-desconocida', clave: valor, real: `${ruta[2]} no está en openapi.json`, candidatos: cercanas }));
          } else if (pedidos.some((p) => !real.metodos.has(p))) {
            out.push(ctx.crear({ ...aqui, tipo: 'metodo-desconocido', clave: valor, real: `${real.ruta} no admite ${pedidos.filter((p) => !real.metodos.has(p)).join('/')}`, esperado: [...real.metodos].join('/') }));
          }
          continue;
        }

        if (/^[a-z][a-z0-9]*\.[a-z0-9-]+$/.test(valor) && gruposPermisos.has(primero(valor)) && !extensionesDeArchivo.has(valor.split('.')[1])) {
          documentados.permisos.add(primero(valor));
          if (!planeada && !permisos.has(valor)) {
            out.push(ctx.crear({ ...aqui, tipo: 'permiso-desconocido', clave: valor, real: `${valor} no está en los catálogos de permisos`, candidatos: parecidos(valor, [...permisos], { tolerancia: 4 }).map((c) => c.nombre) }));
          }
          continue;
        }

        if (/^[A-Z]\w*\.[A-Z]\w*\.[A-Z]\w*$/.test(valor) && gruposCodigos.has(primero(valor))) {
          documentados.codigos.add(primero(valor));
          if (!planeada && !codigos.has(valor)) {
            out.push(ctx.crear({ ...aqui, tipo: 'codigo-desconocido', clave: valor, real: `${valor} no es un código de error del código`, candidatos: parecidos(valor, [...codigos], { tolerancia: 6 }).map((c) => c.nombre) }));
          }
        }
      }
    }
  }

  // Lo que el código define y ninguna feature documenta: una pregunta por grupo, no por elemento.
  const sinDoc = [
    ['permisos', gruposPermisos, 'permiso', 'los permisos'],
    ['codigos', gruposCodigos, 'codigo', 'los códigos de error'],
    ['rutas', gruposRutas, 'ruta', 'las rutas'],
  ];
  for (const [clave, grupos, tipo, descripcion] of sinDoc) {
    for (const grupo of [...grupos].sort()) {
      if (documentados[clave].has(grupo)) continue;
      out.push(ctx.crear({ archivo: 'docs/features', linea: 0, tipo: `${tipo}-sin-feature`, clave: grupo, real: `${descripcion} de «${grupo}» no figuran en ningún docs/features/*.md`, accion: 'humano', pregunta: `¿Qué feature documenta ${descripcion} de «${grupo}», o es a propósito que no estén?` }));
    }
  }
  return out;
}
