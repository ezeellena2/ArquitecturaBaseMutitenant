import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { contar } from './hallazgos.mjs';

export function fechaCorrida(ahora = new Date()) {
  const p = (n) => String(n).padStart(2, '0');
  return `${ahora.getFullYear()}${p(ahora.getMonth() + 1)}${p(ahora.getDate())}-${p(ahora.getHours())}${p(ahora.getMinutes())}`;
}

export function resumenMarkdown(info) {
  const c = info.contadores ?? contar(info.hallazgos ?? []);
  const l = [`# Informe de ${info.arnes} (${info.fecha})`, ''];
  if (info.rama) l.push(`- Rama: \`${info.rama}\``);
  if (info.motor) l.push(`- Motor: ${info.motor}${info.modelo ? ` (${info.modelo})` : ''}`);
  l.push(`- Hallazgos: auto: ${c.auto} · modelo: ${c.modelo} · humano: ${c.humano}`);
  if (info.consumo) l.push(`- Consumo: ${JSON.stringify(info.consumo)}`);
  if (info.estado) l.push(`- Estado: ${info.estado}`);
  const humano = (info.hallazgos ?? []).filter((h) => h.accion === 'humano');
  l.push('', '## Para vos (humano)', '');
  if (!humano.length) l.push('Nada.');
  for (const h of humano) l.push(`- \`${h.archivo}:${h.linea}\` (${h.tipo}): ${h.pregunta ?? h.real}`);
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
