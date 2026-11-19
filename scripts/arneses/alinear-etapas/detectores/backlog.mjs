export const nombre = 'backlog';

const BACKLOG = 'docs/backlog.md';
// Lo que toma una tarea: un plan ejecutable de etapa o una spec. La hoja de ruta y el plan de desarrollo solo la nombran.
const QUIEN_TOMA = /^docs\/plans\/(?:\d{4}-\d{2}-\d{2}-etapa-[^/]+|formularios\/[^/]+)\.md$/;
const VA_ANTES = /segunda parte de la base|antes de la E(1[2-9]|2[0-6])\b/i;

/** El primer párrafo de una sección, si es texto y no una lista ni una tabla. */
function introduccion(seccion) {
  const parrafo = [];
  for (const l of seccion.lineas) {
    if (l.codigo) break;
    if (!l.contenido.trim()) {
      if (parrafo.length) break;
      continue;
    }
    parrafo.push(l.contenido);
  }
  return /^\s*([-*|]|\d+\.)/.test(parrafo[0] ?? '') ? '' : parrafo.join(' ');
}

/**
 * La decisión 214: lo que el backlog pone antes de una etapa pendiente lo toma un plan o una spec.
 * Si no, ninguna etapa lo programa y la etapa siguiente arranca sin eso.
 */
export async function detectar(ctx) {
  if (!ctx.archivos.existe(BACKLOG)) return [];
  const tomadores = [];
  for (const archivo of ctx.archivos.archivos) {
    if (QUIEN_TOMA.test(archivo)) tomadores.push(await ctx.leer(archivo));
  }

  const hallazgos = [];
  for (const seccion of ctx.markdown.secciones(await ctx.leer(BACKLOG))) {
    const va = `${seccion.titulo} ${introduccion(seccion)}`.match(VA_ANTES);
    if (!va || (va[1] && ctx.etapasCerradas.includes(Number(va[1])))) continue;
    const ancla = `backlog.md#${ctx.markdown.slug(seccion.titulo)}`;
    if (tomadores.some((t) => t.includes(ancla) || t.includes(`«${seccion.titulo}»`))) continue;
    const destino = va[1] ? va[0] : `en la ${va[0]}`;
    hallazgos.push(ctx.crear({
      archivo: BACKLOG, linea: seccion.linea, tipo: 'backlog-sin-plan', clave: seccion.titulo,
      real: `«${seccion.titulo}» va ${destino} y ningún plan de etapa ni spec la enlaza: nadie la va a programar`, accion: 'humano',
    }));
  }
  return hallazgos;
}
