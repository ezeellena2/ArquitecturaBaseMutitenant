import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { contar } from './hallazgos.mjs';

export function fechaCorrida(ahora = new Date()) {
  const p = (n) => String(n).padStart(2, '0');
  return `${ahora.getFullYear()}${p(ahora.getMonth() + 1)}${p(ahora.getDate())}-${p(ahora.getHours())}${p(ahora.getMinutes())}`;
}

const formato = (c) => `auto: ${c.auto} · modelo: ${c.modelo} · humano: ${c.humano}`;

export function textoDeConsumo(c) {
  if (!c) return null;
  const partes = [`entrada ${c.tokensEntrada}`];
  if (c.cacheLectura !== undefined) partes.push(`(caché ${c.cacheLectura})`);
  partes.push(`salida ${c.tokensSalida}`);
  if (c.tokensRazonamiento) partes.push(`razonamiento ${c.tokensRazonamiento}`);
  partes.push(c.costoUsd === null || c.costoUsd === undefined ? 'costo n/d' : `costo US$ ${Number(c.costoUsd).toFixed(4)}`);
  return partes.join(' · ');
}

export function resumenMarkdown(info) {
  const inicial = info.contadores ?? contar(info.hallazgos ?? []);
  const l = [`# Informe de ${info.arnes}${info.repo ? ` (${info.repo})` : ''} · ${info.fecha}`, ''];
  if (info.estado) l.push(`- Estado: ${info.estado}`);
  if (info.rama) l.push(`- Rama: \`${info.rama}\``);
  if (info.worktreeConservado && info.worktree) l.push(`- Worktree conservado: \`${info.worktree}\``);
  if (info.motor && info.enviados) l.push(`- Motor: ${info.motor}${info.modelo ? ` (${info.modelo})` : ''} · ${info.enviados} hallazgo(s) enviados`);
  l.push(`- Hallazgos al empezar: ${formato(inicial)}`);
  if (info.contadoresFinales) l.push(`- Hallazgos al terminar: ${formato(info.contadoresFinales)}`);
  if (info.sinEnviar) l.push(`- Quedaron sin enviar al modelo (tope por corrida): ${info.sinEnviar}`);
  const consumo = textoDeConsumo(info.consumo);
  if (consumo) l.push(`- Consumo: ${consumo}`);
  if (info.verificacion?.length) l.push(`- Verificación: ${info.verificacion.map((v) => `${v.codigo === 0 ? 'ok' : 'FALLÓ'} \`${v.comando}\``).join(' · ')}`);
  if (info.revertidos?.length) l.push(`- Revertido por la guardia: ${info.revertidos.map((r) => `\`${r.archivo}\` (${r.motivo})`).join(', ')}`);
  const humano = (info.hallazgos ?? []).filter((h) => h.accion === 'humano');
  l.push('', '## Para vos (humano)', '');
  if (!humano.length) l.push('Nada.');
  for (const h of humano) l.push(`- \`${h.archivo}${h.linea ? `:${h.linea}` : ''}\` (${h.tipo}): ${h.pregunta ?? h.real}`);
  return `${l.join('\n')}\n`;
}

/** Escribe `<dir>/<arnés>/<fecha>.json` y `.md`; devuelve las rutas. */
export async function escribirInforme(dir, info) {
  const base = join(dir, info.arnes);
  await mkdir(base, { recursive: true });
  const json = join(base, `${info.fecha}.json`);
  const md = join(base, `${info.fecha}.md`);
  await writeFile(json, `${JSON.stringify(info, null, 2)}\n`);
  await writeFile(md, resumenMarkdown(info));
  return { json, md };
}
