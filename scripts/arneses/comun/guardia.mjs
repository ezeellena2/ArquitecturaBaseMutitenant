import { rm } from 'node:fs/promises';
import { join } from 'node:path';
import { coincide } from './glob.mjs';
import { git } from './git.mjs';

/** Cambios del worktree respecto de HEAD: [{ archivo, estado: 'modificado'|'creado'|'borrado' }]. */
export async function cambiosDelWorktree(carpeta) {
  const salida = await git(carpeta, ['status', '--porcelain', '-uall']);
  const cambios = [];
  for (const linea of salida.split(/\r?\n/).filter(Boolean)) {
    const codigo = linea.slice(0, 2);
    let archivo = linea.slice(3);
    if (archivo.includes(' -> ')) archivo = archivo.split(' -> ').at(-1);
    archivo = archivo.replace(/^"|"$/g, '');
    let estado = 'modificado';
    if (codigo === '??' || codigo.includes('A')) estado = 'creado';
    else if (codigo.includes('D')) estado = 'borrado';
    cambios.push({ archivo, estado });
  }
  return cambios;
}

/** ¿Está permitido este cambio según el manifiesto? Devuelve el motivo si no. */
export function motivoDeRechazo(cambio, { permisos = {}, excluir = [], propios = [] }) {
  const { archivo, estado } = cambio;
  if (coincide(archivo, permisos.nuncaToca)) return 'está en nuncaToca';
  if (coincide(archivo, excluir) && !propios.includes(archivo)) return 'está en excluir';
  if (estado === 'creado' && !coincide(archivo, permisos.puedeCrear)) return 'no está en puedeCrear';
  if (estado === 'borrado' && !coincide(archivo, permisos.puedeBorrar)) return 'no está en puedeBorrar';
  return null;
}

/**
 * Después del modelo: todo archivo tocado fuera de `permisos` se revierte (D5). Devuelve lo revertido;
 * la corrida falla si hay algo. `propios` son archivos del arnés que el modelo sí puede editar (`ignorados.json`).
 */
export async function guardia(carpeta, { permisos, excluir, propios = [] }) {
  const revertidos = [];
  for (const cambio of await cambiosDelWorktree(carpeta)) {
    const motivo = motivoDeRechazo(cambio, { permisos, excluir, propios });
    if (!motivo) continue;
    if (cambio.estado === 'creado') {
      await git(carpeta, ['reset', '-q', 'HEAD', '--', cambio.archivo]).catch(() => {});
      await rm(join(carpeta, cambio.archivo), { force: true });
    } else {
      await git(carpeta, ['checkout', 'HEAD', '--', cambio.archivo]);
    }
    revertidos.push({ archivo: cambio.archivo, estado: cambio.estado, motivo });
  }
  return { ok: revertidos.length === 0, revertidos };
}
