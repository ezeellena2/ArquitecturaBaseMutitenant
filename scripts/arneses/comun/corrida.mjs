import { existsSync } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, isAbsolute, join, relative } from 'node:path';
import { cambiosDelWorktree, guardia } from './guardia.mjs';
import { git } from './git.mjs';
import { contar } from './hallazgos.mjs';
import { escribirInforme, fechaCorrida } from './informe.mjs';
import { aplicarParches } from './parches.mjs';
import { ejecutarProceso } from './proceso.mjs';
import { validar } from './resultado.mjs';
import { carpetaDelWorktree, crearWorktree, enlazarNodeModules, nombreDeRama, quitarWorktree } from './worktree.mjs';
import * as claude from './motores/claude.mjs';
import * as codex from './motores/codex.mjs';

const motores = { claude, codex };

async function leerJson(ruta, porDefecto) {
  try {
    return JSON.parse(await readFile(ruta, 'utf8'));
  } catch {
    return porDefecto;
  }
}

const pocos = (texto, n = 600) => (texto.length > n ? `…${texto.slice(-n)}` : texto);

/**
 * Corrida completa de un arnés sobre un repo (plan §6, paso 3): worktree → detectar → auto → modelo → guardia →
 * volver a detectar → verificar → commitear → informe. `detectar` se inyecta para no depender de arnes.mjs.
 */
export async function corridaDeRepo({ detectar, arnes, repo, dirArneses, raices, opciones, dirInformes, historial, bins, escribir, raizBackArneses }) {
  const { manifiesto } = arnes;
  const raiz = raices[repo];
  const fecha = opciones.fecha ?? fechaCorrida();
  const rama = nombreDeRama(arnes.carpeta, fecha);
  const carpeta = carpetaDelWorktree(raiz, arnes.carpeta, fecha);
  const info = { arnes: arnes.carpeta, repo, fecha: `${fecha}-${repo}`, modo: 'corrida', rama, worktree: carpeta, estado: 'en curso', hallazgos: [] };
  const dirTareas = join(carpeta, '.local', 'arneses', arnes.carpeta);

  await crearWorktree(raiz, { rama, carpeta });
  // El código del arnés y su `ignorados.json` se leen del worktree cuando el arnés vive en este mismo repo.
  const rel = relative(raizBackArneses ?? raiz, dirArneses);
  const dirDet = !rel.startsWith('..') && !isAbsolute(rel) && raiz === (raizBackArneses ?? raiz) ? join(carpeta, rel) : dirArneses;
  const raicesWt = { ...raices, [repo]: carpeta };
  const detectarAqui = () => detectar({ dirArneses: dirDet, arnes, raices: raicesWt, soloRepo: repo, historial });

  let hallazgos = await detectarAqui();
  info.contadores = contar(hallazgos);
  escribir(`${repo}: auto: ${info.contadores.auto} · modelo: ${info.contadores.modelo} · humano: ${info.contadores.humano}`);

  // 1. Lo mecánico, sin modelo.
  const autos = hallazgos.filter((h) => h.accion === 'auto' && h.parche);
  if (autos.length) {
    const r = await aplicarParches(carpeta, autos.map((h) => h.parche), manifiesto.permisos);
    info.autosAplicados = r.aplicados.length;
    info.autosFallidos = r.fallidos;
    hallazgos = await detectarAqui();
  }

  // 2. Lo que pide criterio, con el motor elegido y solo si hay algo para mandarle.
  const motorNombre = opciones.motor ?? manifiesto.modelo?.motor ?? 'claude';
  const cfg = manifiesto.modelo?.[motorNombre] ?? {};
  const modeloNombre = opciones.modelo ?? cfg.nombre;
  info.motor = motorNombre;
  info.modelo = modeloNombre;
  const maximo = manifiesto.modelo?.maxHallazgos ?? 40;
  const candidatos = hallazgos.filter((h) => h.accion === 'modelo');
  const enviados = opciones.sinModelo ? [] : candidatos.slice(0, maximo);
  info.enviados = enviados.length;
  info.sinEnviar = candidatos.length - enviados.length;
  let resultados = [];
  let fallo = null;

  if (enviados.length) {
    const tareaRel = `.local/arneses/${arnes.carpeta}/${fecha}.modelo.json`;
    await mkdir(dirTareas, { recursive: true });
    const tarea = { arnes: arnes.carpeta, repo, hallazgos: enviados.map(({ parche, ...h }) => h) };
    await writeFile(join(carpeta, tareaRel), `${JSON.stringify(tarea, null, 2)}\n`);
    await mkdir(join(dirInformes, arnes.carpeta), { recursive: true });
    await writeFile(join(dirInformes, arnes.carpeta, `${info.fecha}.modelo.json`), `${JSON.stringify(tarea, null, 2)}\n`);

    const instrucciones = join(dirDet, arnes.carpeta, 'instrucciones.md');
    const esquema = join(dirDet, arnes.carpeta, 'resultado.schema.json');
    const prompt = `Procesá los hallazgos de ${tareaRel} y respondé solo con el JSON final.`;
    const aislar = opciones.sinAislar !== true;
    const motor = motores[motorNombre];
    if (!motor) throw new Error(`Motor desconocido: ${motorNombre}`);
    const r = motorNombre === 'claude'
      ? await claude.ejecutar({ tarea: prompt, instrucciones, modelo: modeloNombre, esfuerzo: cfg.esfuerzo ?? 'low', presupuestoUsd: opciones.presupuesto ?? cfg.presupuestoUsd, minutosMaximos: cfg.minutosMaximos ?? 15, cwd: carpeta, aislar, bin: bins?.claude })
      : await codex.ejecutar({ tarea: prompt, instruccionesTexto: await readFile(instrucciones, 'utf8'), modelo: modeloNombre, esfuerzo: cfg.esfuerzo ?? 'low', minutosMaximos: cfg.minutosMaximos ?? 10, cwd: carpeta, esquema, salida: join(dirTareas, `${fecha}.resultado.json`), aislar, bin: bins?.codex });
    info.consumo = r.consumo;
    info.estadoMotor = r.estado;
    if (!r.ok) fallo = `el motor ${motorNombre} falló: ${r.estado}`;
    else {
      const errores = validar(r.resultado, await leerJson(esquema, {}));
      if (errores.length) fallo = `la respuesta del modelo no cumple resultado.schema.json: ${errores.slice(0, 3).join('; ')}`;
      else resultados = r.resultado.resultados;
    }
  }

  // 3. Guardia: lo tocado fuera de `permisos` se revierte y la corrida falla.
  const g = await guardia(carpeta, { permisos: manifiesto.permisos, excluir: manifiesto.excluir ?? [] });
  info.revertidos = g.revertidos;
  if (!g.ok) fallo ??= `la guardia revirtió ${g.revertidos.length} archivo(s) fuera de permisos: ${g.revertidos.map((x) => x.archivo).join(', ')}`;

  // 4. Lo que el modelo descartó se anota en ignorados.json (lo escribe el script, no el modelo).
  const descartados = resultados.filter((x) => x.resultado === 'descartado');
  if (descartados.length) {
    const archivoIgnorados = join(dirDet, arnes.carpeta, 'ignorados.json');
    const actuales = await leerJson(archivoIgnorados, []);
    const hoy = new Date().toISOString().slice(0, 10);
    for (const d of descartados) actuales.push({ id: d.id, motivo: d.nota || 'descartado por el modelo', fecha: hoy });
    await writeFile(archivoIgnorados, `${JSON.stringify(actuales, null, 2)}\n`);
  }

  // 5. Volver a detectar: no puede quedar ningún `auto`, y lo que quedó de `modelo` se reporta para la persona.
  hallazgos = await detectarAqui();
  const sobrantes = hallazgos.filter((h) => h.accion === 'auto');
  if (sobrantes.length) fallo ??= `quedaron ${sobrantes.length} hallazgo(s) auto sin resolver: ${sobrantes.slice(0, 3).map((h) => h.id).join('; ')}`;
  const escalados = new Map(resultados.filter((x) => x.resultado === 'humano').map((x) => [x.id, x.nota]));
  const idsEnviados = new Set(enviados.map((h) => h.id));
  info.hallazgos = hallazgos.map((h) => {
    if (escalados.has(h.id)) return { ...h, accion: 'humano', pregunta: escalados.get(h.id) };
    if (idsEnviados.has(h.id)) return { ...h, accion: 'humano', pregunta: `el modelo no lo resolvió: ${h.real}` };
    return h;
  });
  info.contadoresFinales = contar(info.hallazgos);

  // 6. Verificación, solo si todo lo anterior salió bien.
  info.verificacion = [];
  if (!fallo) {
    if (repo === 'front') await enlazarNodeModules(raiz, carpeta);
    for (const comando of manifiesto.verificacion?.[repo] ?? []) {
      const v = await ejecutarProceso(comando, { cwd: carpeta, shell: true, timeoutMs: 30 * 60_000 });
      info.verificacion.push({ comando, codigo: v.codigo, salida: v.codigo === 0 ? '' : pocos(`${v.stdout}\n${v.stderr}`) });
      if (v.codigo !== 0) {
        fallo = `la verificación falló: ${comando}`;
        break;
      }
    }
  }

  // 7. Commit en la rama del arnés (nunca push).
  if (fallo) {
    info.estado = `falló: ${fallo}`;
    info.worktreeConservado = true;
  } else {
    const cambios = await cambiosDelWorktree(carpeta);
    if (!cambios.length) {
      info.estado = 'sin cambios';
      await quitarWorktree(raiz, carpeta);
      await git(raiz, ['branch', '-D', rama]).catch(() => {});
      info.rama = null;
    } else {
      const c = info.contadores;
      const cuerpo = [`Arnés: ${arnes.carpeta} (${repo}).`, `Hallazgos: auto ${c.auto} · modelo ${c.modelo} · humano ${c.humano}.`, info.enviados ? `Modelo: ${motorNombre}${modeloNombre ? ` (${modeloNombre})` : ''}, ${info.enviados} hallazgo(s).` : 'Sin llamada al modelo.']
        .join('\n');
      await git(carpeta, ['add', '-A']);
      await git(carpeta, ['commit', '-q', '-m', `${manifiesto.commit}\n\n${cuerpo}`]);
      info.commit = (await git(carpeta, ['rev-parse', 'HEAD'])).trim();
      info.estado = 'commiteado';
      if (opciones.fusionar) {
        try {
          await git(raiz, ['merge', '--ff-only', rama]);
          info.estado = 'fusionado';
        } catch (e) {
          info.estado = `commiteado (no se pudo fusionar con --ff-only: ${String(e.message).split('\n')[0]})`;
        }
      }
      await quitarWorktree(raiz, carpeta);
      info.worktree = null;
    }
  }

  const rutas = await escribirInforme(dirInformes, info);
  info.informe = rutas.md;
  return info;
}
