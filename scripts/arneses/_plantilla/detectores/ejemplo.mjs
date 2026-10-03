export const nombre = 'ejemplo';
// Opcional: export const repos = ['back'];

/**
 * Un detector solo lee: nunca escribe ni llama al modelo.
 * ctx: { repo, raiz, archivos, markdown, etapasCerradas, manifiesto, excluido(rel), leer(rel), crear(campos) }
 * @returns {Promise<object[]>} hallazgos creados con ctx.crear({ archivo, linea, tipo, clave, real, esperado, accion, parche })
 */
export async function detectar(ctx) {
  const hallazgos = [];
  for (const archivo of ctx.archivos.archivos.filter((a) => a.endsWith('.md') && !ctx.excluido(a))) {
    const texto = await ctx.leer(archivo);
    if (texto.includes('TODO-ARNES')) {
      hallazgos.push(ctx.crear({ archivo, linea: 1, tipo: 'marca-pendiente', clave: 'TODO-ARNES', real: 'quedó una marca TODO-ARNES', accion: 'humano', pregunta: '¿Qué va en lugar de TODO-ARNES?' }));
    }
  }
  return hallazgos;
}
