import { CARPETA, INDICE, LIENZO, enPalabras, esCodigoDeAlla, esTablero, leerIndice } from '../etapas.mjs';

export const nombre = 'tableros';

/** El índice de specs contra el lienzo del front: lo que dice cada etapa de sus tableros, contra lo que existe. */
export async function detectar(ctx) {
  const hallazgos = [];
  const anotar = (fila, tipo, clave, real) => hallazgos.push(ctx.crear({ archivo: INDICE, linea: fila.linea, tipo, clave: `E${fila.numero}:${clave}`, real, accion: 'humano' }));
  const existe = (tablero) => ctx.hermano.existe(`${LIENZO}/${tablero}.dc.html`);

  for (const fila of await leerIndice(ctx)) {
    const etapa = `E${fila.numero}`;
    if (!fila.tramos.length) {
      anotar(fila, 'fila-sin-etiqueta', 'fila', `${etapa}: la fila no dice qué tableros están aprobados, a revisar, a redibujar o por dibujar`);
      continue;
    }
    for (const { etiqueta, nombres } of fila.tramos) {
      if (!ctx.hermano) break;
      for (const tablero of nombres.filter(esTablero)) {
        if (etiqueta === 'Por dibujar' && existe(tablero)) anotar(fila, 'tablero-ya-existe', tablero, `${etapa}: ${tablero} figura por dibujar y ya está en el lienzo`);
        if (etiqueta !== 'Por dibujar' && !existe(tablero)) anotar(fila, 'tablero-no-existe', tablero, `${etapa}: ${tablero} figura «${etiqueta}» y no está en el lienzo`);
      }
    }
    const porDibujar = fila.tramos.filter((t) => t.etiqueta === 'Por dibujar').flatMap((t) => t.nombres);
    const codigos = porDibujar.filter(esCodigoDeAlla);
    if (codigos.length) anotar(fila, 'tablero-sin-nombre', 'codigos', `${etapa}: sin nombre de tablero: ${enPalabras(codigos)}`);
    const conPorDibujar = fila.tramos.some((t) => t.etiqueta === 'Por dibujar');
    const texto = `${CARPETA}/${String(fila.numero).padStart(2, '0')}-texto-diseno.md`;
    if (conPorDibujar && !ctx.archivos.existe(texto)) anotar(fila, 'falta-texto-diseno', 'texto', `${etapa}: tiene tableros por dibujar y no tiene su texto para Diseño`);
  }
  return hallazgos;
}
