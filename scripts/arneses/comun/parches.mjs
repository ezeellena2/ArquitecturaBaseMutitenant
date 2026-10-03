import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { existsSync } from 'node:fs';
import { coincide } from './glob.mjs';

/**
 * Parches genéricos: reemplazar, borrar-linea, insertar-linea, crear-archivo, escribir-archivo, borrar-archivo.
 * Verifican el texto que esperan y los permisos del manifiesto (D4, D5); lo que no coincide queda en `fallidos`.
 */

function permitido(parche, permisos = {}) {
  const { archivo } = parche;
  if (coincide(archivo, permisos.nuncaToca)) return 'está en nuncaToca';
  if (parche.tipo === 'crear-archivo' && !coincide(archivo, permisos.puedeCrear)) return 'no está en puedeCrear';
  if (parche.tipo === 'borrar-archivo' && !coincide(archivo, permisos.puedeBorrar)) return 'no está en puedeBorrar';
  return null;
}

function dividir(texto) {
  const eol = texto.includes('\r\n') ? '\r\n' : '\n';
  const finalNuevaLinea = texto.endsWith('\n');
  const lineas = texto.split(/\r?\n/);
  if (finalNuevaLinea) lineas.pop();
  return { eol, lineas, finalNuevaLinea };
}

const unir = ({ eol, lineas, finalNuevaLinea }) => lineas.join(eol) + (finalNuevaLinea ? eol : '');

function ordenDeAplicacion(p) {
  return p.tipo === 'insertar-linea' ? p.despues + 0.5 : (p.linea ?? 0);
}

export async function aplicarParches(raiz, parches, permisos) {
  const aplicados = [];
  const fallidos = [];
  const fallar = (parche, motivo) => fallidos.push({ parche, motivo });

  const deArchivo = new Map();
  const deArchivoCompleto = [];
  for (const p of parches) {
    const razon = permitido(p, permisos);
    if (razon) {
      fallar(p, `sin permiso: ${p.archivo} ${razon}`);
      continue;
    }
    if (['crear-archivo', 'escribir-archivo', 'borrar-archivo'].includes(p.tipo)) deArchivoCompleto.push(p);
    else {
      if (!deArchivo.has(p.archivo)) deArchivo.set(p.archivo, []);
      deArchivo.get(p.archivo).push(p);
    }
  }

  for (const [archivo, lista] of deArchivo) {
    const ruta = join(raiz, archivo);
    if (!existsSync(ruta)) {
      for (const p of lista) fallar(p, `no coincide: ${archivo} no existe`);
      continue;
    }
    const doc = dividir(await readFile(ruta, 'utf8'));
    let cambio = false;
    for (const p of [...lista].sort((a, b) => ordenDeAplicacion(b) - ordenDeAplicacion(a))) {
      if (p.tipo === 'insertar-linea') {
        if (p.despues < 0 || p.despues > doc.lineas.length) fallar(p, `no coincide: línea ${p.despues} fuera de rango`);
        else {
          doc.lineas.splice(p.despues, 0, p.texto);
          aplicados.push(p);
          cambio = true;
        }
        continue;
      }
      const actual = doc.lineas[p.linea - 1];
      if (actual === undefined || !actual.includes(p.de)) {
        fallar(p, `no coincide: la línea ${p.linea} de ${archivo} no contiene «${p.de}»`);
        continue;
      }
      if (p.tipo === 'reemplazar') doc.lineas[p.linea - 1] = actual.replace(p.de, () => p.a);
      else if (p.tipo === 'borrar-linea') doc.lineas.splice(p.linea - 1, 1);
      else {
        fallar(p, `no coincide: tipo de parche desconocido ${p.tipo}`);
        continue;
      }
      aplicados.push(p);
      cambio = true;
    }
    if (cambio) await writeFile(ruta, unir(doc));
  }

  for (const p of deArchivoCompleto) {
    const ruta = join(raiz, p.archivo);
    if (p.tipo === 'crear-archivo') {
      if (existsSync(ruta)) {
        fallar(p, `no coincide: ${p.archivo} ya existe`);
        continue;
      }
      await mkdir(dirname(ruta), { recursive: true });
      await writeFile(ruta, p.contenido);
    } else if (p.tipo === 'escribir-archivo') {
      if (!existsSync(ruta)) {
        fallar(p, `no coincide: ${p.archivo} no existe`);
        continue;
      }
      await writeFile(ruta, p.contenido);
    } else {
      if (!existsSync(ruta)) {
        fallar(p, `no coincide: ${p.archivo} no existe`);
        continue;
      }
      await rm(ruta);
    }
    aplicados.push(p);
  }
  return { aplicados, fallidos };
}
