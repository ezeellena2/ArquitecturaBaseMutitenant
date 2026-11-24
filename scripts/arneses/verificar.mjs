#!/usr/bin/env node
/**
 * El build y los tests del back con la salida mínima (ADR 0071): muestra solo el resumen y lo que falló, y deja
 * el log entero en .local/verificar/. Lo usan los agentes del conductor para no llenar su contexto con la consola.
 *
 *   node scripts/arneses/verificar.mjs                       build y tests rápidos (Domain, Application, Architecture)
 *   node scripts/arneses/verificar.mjs --sin-build           solo los tests rápidos
 *   node scripts/arneses/verificar.mjs --proyecto Application.UnitTests [--clase <Clase>]...
 *   node scripts/arneses/verificar.mjs --integracion [--clase <Clase>]... [--espacio <Namespace>]...
 *
 * La integración usa Docker y espera su turno en la fila pesada (carril.mjs), así corre una por vez aunque haya
 * varios carriles. Sale 0 si todo pasó y 1 si algo falló.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { conCandado, raizDe } from './carril.mjs';

const SOLUCION = 'ArquitecturaBaseMultitenant.slnx';
export const RAPIDOS = ['Domain.UnitTests', 'Application.UnitTests', 'ArchitectureTests'];
const INTEGRACION = 'Api.IntegrationTests';
const MAXIMO_DE_FALLOS = 15;
const LINEAS_POR_FALLO = 6;

const proyecto = (nombre) => `tests/ArquitecturaBaseMultitenant.${nombre}/ArquitecturaBaseMultitenant.${nombre}.csproj`;

export function parsear(argv) {
  const o = { build: true, proyectos: RAPIDOS, clases: [], espacios: [], integracion: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--sin-build') o.build = false;
    else if (a === '--integracion') { o.integracion = true; o.proyectos = [INTEGRACION]; }
    else if (a === '--proyecto') o.proyectos = [argv[++i].replace(/^ArquitecturaBaseMultitenant\./, '')];
    else if (a === '--clase') o.clases.push(argv[++i]);
    else if (a === '--espacio') o.espacios.push(argv[++i]);
    else throw new Error(`Opción desconocida: ${a}`);
  }
  if ((o.clases.length || o.espacios.length) && o.proyectos.length > 1) throw new Error('--clase y --espacio van con --proyecto o con --integracion.');
  return o;
}

/** Los comandos que corre, en orden. */
export function planDe(o) {
  const pasos = [];
  if (o.build) pasos.push({ nombre: 'build', args: ['build', SOLUCION, '-nologo', '-v', 'q', '-clp:NoSummary'] });
  const filtros = [...o.clases.flatMap((c) => ['--filter-class', c]), ...o.espacios.flatMap((e) => ['--filter-namespace', e])];
  for (const p of o.proyectos) {
    pasos.push({ nombre: p, args: ['test', '--project', proyecto(p), '--no-build', ...(filtros.length ? ['--', ...filtros] : [])] });
  }
  return pasos;
}

export function erroresDeBuild(texto) {
  const vistos = new Set();
  for (const linea of texto.split(/\r?\n/)) {
    const m = linea.match(/^\s*(.+?: (?:error|warning) [A-Z]+\d+: .+?)(?: \[[^\]]+\])?\s*$/);
    if (m) vistos.add(m[1]);
  }
  return [...vistos];
}

const LINEA_DE_FALLO = /^\s*(?:failed|con errores)\s+(\S+)\s+\(([^)]*)\)\s*$/i;

export function fallosDeTests(texto) {
  const lineas = texto.split(/\r?\n/);
  const fallos = [];
  for (let i = 0; i < lineas.length; i++) {
    const m = lineas[i].match(LINEA_DE_FALLO);
    if (!m) continue;
    const detalle = [];
    for (let j = i + 1; j < lineas.length && detalle.length < LINEAS_POR_FALLO; j++) {
      const l = lineas[j].trim();
      if (LINEA_DE_FALLO.test(lineas[j]) || /^(Stack Trace|Seguimiento de la pila|Resumen de la serie|Test run summary)/i.test(l)) break;
      if (LINEA_DE_FALLO.test(lineas[j]) || /\(net\d+\.\d+\|[^)]*\) (error|failed)/i.test(l)) break;
      if (!l || /^(at|a las|en|desde|from) /.test(l) || /^--- End of/.test(l)) continue;
      detalle.push(l.length > 300 ? `${l.slice(0, 300)}…` : l);
    }
    fallos.push({ test: m[1], tiempo: m[2], detalle });
  }
  return fallos;
}

export function resumenDeTests(texto) {
  const n = (re) => { const m = texto.match(re); return m ? Number(m[1]) : null; };
  return {
    total: n(/^\s*total:\s*(\d+)/im),
    fallaron: n(/^\s*(?:failed|error):\s*(\d+)/im),
    pasaron: n(/^\s*(?:succeeded|correcto):\s*(\d+)/im),
    omitidos: n(/^\s*(?:skipped|omitido):\s*(\d+)/im),
  };
}

/** Lo que se muestra de un paso: una línea si pasó; lo que falló, si no. */
export function informe(paso, codigo, texto, segundos) {
  if (paso === 'build') {
    if (codigo === 0) return [`build: ok (${segundos} s)`];
    const errores = erroresDeBuild(texto);
    return [`build: ${errores.length || 'con'} error(es) (${segundos} s)`, ...errores.slice(0, 20).map((e) => `  ${e}`), ...(errores.length ? [] : ultimas(texto))];
  }
  const r = resumenDeTests(texto);
  const cuenta = r.total === null ? '' : `${r.pasaron ?? '?'} de ${r.total}${r.omitidos ? `, ${r.omitidos} omitidos` : ''}`;
  if (codigo === 0) return [`${paso}: ok, ${cuenta || 'sin resumen'} (${segundos} s)`];
  const fallos = fallosDeTests(texto);
  const salida = [`${paso}: ${r.fallaron ?? fallos.length} con error${cuenta ? `, ${cuenta} pasaron` : ''} (${segundos} s)`];
  for (const f of fallos.slice(0, MAXIMO_DE_FALLOS)) salida.push(`  ✖ ${f.test} (${f.tiempo})`, ...f.detalle.map((d) => `      ${d}`));
  if (fallos.length > MAXIMO_DE_FALLOS) salida.push(`  … y ${fallos.length - MAXIMO_DE_FALLOS} más en el log`);
  if (!fallos.length) salida.push(...ultimas(texto));
  return salida;
}

function ultimas(texto) {
  return ['  (sin fallos reconocibles; las últimas líneas:)', ...texto.trim().split(/\r?\n/).slice(-25).map((l) => `  ${l}`)];
}

/** En una copia de un carril, origin es la carpeta de main: la fila pesada vive allá, compartida por todos. */
function raizDeLaFila(raiz) {
  const r = spawnSync('git', ['remote', 'get-url', 'origin'], { cwd: raiz, encoding: 'utf8' });
  const url = (r.stdout || '').trim();
  return url && existsSync(join(url, '.git')) ? resolve(url) : raiz;
}

export async function ejecutar(argv, { cwd = process.cwd(), escribir = (s) => process.stdout.write(`${s}\n`), correr = correrDotnet } = {}) {
  let o;
  try { o = parsear(argv); } catch (e) { escribir(e.message); return 1; }
  const raiz = raizDe(cwd);
  const logs = join(raiz, '.local', 'verificar');
  mkdirSync(logs, { recursive: true });
  const todo = async () => {
    if (o.integracion) {
      const d = spawnSync('docker', ['info', '--format', '{{.ServerVersion}}'], { encoding: 'utf8', windowsHide: true });
      if (d.status !== 0) { escribir('Docker no responde: la integración necesita Docker Desktop abierto.'); return 1; }
    }
    let codigoFinal = 0;
    for (const paso of planDe(o)) {
      const desde = Date.now();
      const { codigo, texto } = correr(paso.args, raiz);
      const log = join(logs, `${paso.nombre}.log`);
      writeFileSync(log, texto);
      for (const l of informe(paso.nombre, codigo, texto, Math.round((Date.now() - desde) / 1000))) escribir(l);
      if (codigo !== 0) {
        codigoFinal = 1;
        escribir(`Log entero: ${log}`);
        if (paso.nombre === 'build') break;
      }
    }
    return codigoFinal;
  };
  return o.integracion ? conCandado(raizDeLaFila(raiz), 'pesada', todo, { avisar: escribir }) : todo();
}

function correrDotnet(args, cwd) {
  const r = spawnSync('dotnet', args, { cwd, encoding: 'utf8', windowsHide: true, maxBuffer: 1024 ** 3 });
  return { codigo: r.status ?? 1, texto: `${r.stdout || ''}${r.stderr || ''}` };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await ejecutar(process.argv.slice(2));
}
