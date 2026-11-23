import { etapaDe, hallazgo } from '../etapa.mjs';
import { cambiosDeMigracion, esMigracion } from '../extraer.mjs';

export const nombre = 'tablas';
export const repos = ['back'];

/** Cada tabla que crea una migración nueva de la etapa, y cada columna que suma, está en la spec: la columna, en el bloque de su tabla. */
export async function detectar(ctx) {
  const etapa = await etapaDe(ctx);
  const { spec } = etapa;
  const out = new Map();
  const marcar = (tipo, nombreCompleto, commit, archivo, linea) => {
    const clave = `${tipo}:${nombreCompleto}`;
    if (!out.has(clave)) out.set(clave, hallazgo(ctx, etapa, { tipo, nombre: nombreCompleto, commit, archivo, linea }));
  };
  for (const commit of etapa.commits) {
    for (const archivo of commit.archivos) {
      if (!archivo.nuevo || !esMigracion(archivo.ruta)) continue;
      const { tablas, columnas } = cambiosDeMigracion(archivo);
      for (const t of tablas) {
        if (!spec.tabla(t.tabla)) {
          marcar('tabla', t.esquema ? `${t.esquema}.${t.tabla}` : t.tabla, commit, archivo.ruta, t.linea);
          continue;
        }
        for (const c of t.columnas) if (!spec.columna(t.tabla, c.nombre)) marcar('columna', `${t.tabla}.${c.nombre}`, commit, archivo.ruta, c.linea);
      }
      for (const c of columnas) if (!spec.columna(c.tabla, c.nombre)) marcar('columna', `${c.tabla}.${c.nombre}`, commit, archivo.ruta, c.linea);
    }
  }
  return [...out.values()];
}
