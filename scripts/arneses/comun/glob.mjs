/** Glob mínimo: `**` cruza carpetas, `*` no, `?` un carácter. Las rutas van con `/`. */
export function globARegex(patron) {
  let re = '';
  for (let i = 0; i < patron.length; i += 1) {
    const c = patron[i];
    if (c === '*' && patron[i + 1] === '*') {
      if (patron[i + 2] === '/') {
        re += '(?:.*/)?';
        i += 2;
      } else {
        re += '.*';
        i += 1;
      }
    } else if (c === '*') re += '[^/]*';
    else if (c === '?') re += '[^/]';
    else re += c.replace(/[.+^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp(`^${re}$`);
}

export function coincide(rel, patrones = []) {
  return patrones.some((p) => globARegex(p).test(rel));
}
