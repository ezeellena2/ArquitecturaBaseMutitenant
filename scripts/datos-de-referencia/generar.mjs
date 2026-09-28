import { createHash } from 'node:crypto';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { XMLParser } from 'fast-xml-parser';
import { getCountryCallingCode } from 'libphonenumber-js';

const scriptRoot = dirname(fileURLToPath(import.meta.url));
const outputRoot = resolve(scriptRoot, '../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceData');
const xmlParser = new XMLParser({
  ignoreAttributes: false,
  attributeNamePrefix: '',
  parseTagValue: false,
  parseAttributeValue: false,
  processEntities: false,
  ignoreDeclaration: true,
  ignorePiTags: true,
  trimValues: true
});

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

async function readJson(path) {
  return JSON.parse(await readFile(path, 'utf8'));
}

function stableJson(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function mergeTree(target, source) {
  for (const [key, value] of Object.entries(source)) {
    target[key] = value && typeof value === 'object' && !Array.isArray(value)
      ? mergeTree({ ...(target[key] ?? {}) }, value)
      : value;
  }
  return target;
}

function sourcePath(root, entry) {
  assert(entry && typeof entry.path === 'string' && typeof entry.url === 'string' && typeof entry.version === 'string' && /^[a-f0-9]{64}$/.test(entry.sha256), 'sources.lock.json contiene una fuente incompleta.');
  const path = resolve(root, entry.path);
  assert(!isAbsolute(entry.path) && !relative(root, path).startsWith('..'), `Ruta de fuente fuera del proyecto: ${entry.path}`);
  return path;
}

async function verifiedSource(root, entry) {
  const path = sourcePath(root, entry);
  let bytes;
  try {
    bytes = await readFile(path);
  } catch (error) {
    if (error.code === 'ENOENT') throw new Error(`${entry.path} no existe. Ejecutá --refresh para actualizar el snapshot.`, { cause: error });
    throw error;
  }
  assert(sha256(bytes) === entry.sha256, `SHA-256 incorrecto para ${entry.path}.`);
  return bytes.toString('utf8');
}

function parseIso(xml) {
  const document = xmlParser.parse(xml);
  const root = document.ISO_4217;
  assert(root?.Pblshd && root?.CcyTbl?.CcyNtry, 'SIX List One no tiene la estructura ISO_4217/CcyTbl/CcyNtry esperada.');
  const byCode = new Map();
  for (const entry of [].concat(root.CcyTbl.CcyNtry)) {
    const code = entry.Ccy;
    if (!code) continue;
    assert(/^[A-Z]{3}$/.test(code), `Código de SIX inválido: ${code}`);
    const numericCode = entry.CcyNbr ?? null;
    const minorText = entry.CcyMnrUnts ?? null;
    const minorUnits = /^[0-9]+$/.test(minorText ?? '') ? Number(minorText) : null;
    const officialName = typeof entry.CcyNm === 'string' ? entry.CcyNm : entry.CcyNm?.['#text'];
    const current = { Code: code, NumericCode: numericCode, MinorUnits: minorUnits, Name: officialName ?? code };
    const previous = byCode.get(code);
    if (previous) {
      assert(previous.NumericCode === current.NumericCode && previous.MinorUnits === current.MinorUnits, `${code} tiene datos contradictorios entre entidades en SIX List One.`);
    } else {
      byCode.set(code, current);
    }
  }
  return { version: root.Pblshd, byCode };
}

function ordinal(code) {
  return (code.charCodeAt(0) - 65) * 26 + code.charCodeAt(1) - 65;
}

function fromOrdinal(number) {
  return String.fromCharCode(65 + Math.floor(number / 26), 65 + number % 26);
}

function parseRegularRegions(xml) {
  const document = xmlParser.parse(xml);
  const entries = [].concat(document.supplementalData?.idValidity?.id ?? []);
  const regular = entries.find(entry => entry.type === 'region' && entry.idStatus === 'regular');
  assert(regular, 'CLDR no contiene regiones con idStatus=regular.');
  const tokens = String(regular['#text'] ?? '').trim().split(/\s+/);
  const result = new Set();
  for (const token of tokens) {
    const [start, suffix, unexpected] = token.split('~');
    assert(!unexpected && /^[A-Z]{2}$/.test(start), `Rango de región CLDR inválido: ${token}`);
    if (!suffix) {
      result.add(start);
      continue;
    }
    assert(/^[A-Z]{1,2}$/.test(suffix), `Rango de región CLDR inválido: ${token}`);
    const end = start.slice(0, 2 - suffix.length) + suffix;
    assert(ordinal(end) >= ordinal(start), `Rango de región CLDR invertido: ${token}`);
    for (let index = ordinal(start); index <= ordinal(end); index++) result.add(fromOrdinal(index));
  }
  return result;
}

function parseIanaZones(tab, countrySet = null) {
  const zones = [];
  const seen = new Set();
  for (const line of tab.split(/\r?\n/)) {
    if (!line || line.startsWith('#')) continue;
    const [countriesText, , id] = line.split('\t');
    assert(countriesText && id && /^[A-Za-z0-9_+./-]+$/.test(id), `Fila IANA inválida: ${line}`);
    const countryCodes = countriesText.split(',');
    assert(countryCodes.every(code => /^[A-Z]{2}$/.test(code)) && new Set(countryCodes).size === countryCodes.length,
      `CountryCodes inválidos en IANA para ${id}.`);
    for (const code of countryCodes) {
      if (countrySet) assert(countrySet.has(code), `${code} de IANA no tiene país ISO (FK rota en ${id}).`);
    }
    assert(!seen.has(id), `Zona IANA duplicada: ${id}`);
    seen.add(id);
    zones.push({ Id: id, CountryCodes: countryCodes });
  }
  assert(zones.length > 0, 'IANA zone1970.tab no contiene zonas.');
  assert(!seen.has('UTC'), 'UTC ya aparece en IANA zone1970.tab.');
  zones.push({ Id: 'UTC', CountryCodes: [] });
  return zones;
}

function currentTenderCurrency(entries, asOf) {
  for (const row of [].concat(entries ?? [])) {
    const [code, attributes] = Object.entries(row)[0] ?? [];
    if (!code) continue;
    const from = attributes?._from?.slice(0, 10);
    const to = attributes?._to?.slice(0, 10);
    if (attributes?._tender !== 'false' && (!from || from <= asOf) && (!to || to > asOf)) return code;
  }
  return null;
}

function validateOverrideMap(config, key, codes) {
  const overrides = config[key] ?? {};
  assert(typeof overrides === 'object' && !Array.isArray(overrides), `${key} debe ser un objeto.`);
  for (const [code, value] of Object.entries(overrides)) {
    assert(codes.has(code), `${code} es un override desconocido en ${key}.`);
    assert(value && typeof value === 'object' && !Array.isArray(value), `${key}.${code} debe ser un objeto.`);
    for (const field of Object.keys(value)) assert(field === 'isEnabled' || field === 'sortOrder', `${key}.${code}.${field} no es un override conocido.`);
    if ('isEnabled' in value) assert(typeof value.isEnabled === 'boolean', `${key}.${code}.isEnabled debe ser booleano.`);
    if ('sortOrder' in value) assert(Number.isInteger(value.sortOrder), `${key}.${code}.sortOrder debe ser entero.`);
  }
  return overrides;
}

function selection(code, defaultCode, overrides) {
  const override = overrides[code] ?? {};
  return {
    IsEnabled: override.isEnabled ?? code === defaultCode,
    SortOrder: override.sortOrder ?? null
  };
}

async function cldrLocale(cldrRoot, packageName, locale, fileName, select) {
  const parts = locale.split('-');
  const merged = {};
  let found = false;
  for (let count = 1; count <= parts.length; count++) {
    const candidate = parts.slice(0, count).join('-');
    try {
      const document = await readJson(join(cldrRoot, packageName, 'main', candidate, fileName));
      const values = select(document.main[candidate]);
      mergeTree(merged, values);
      found = true;
    } catch (error) {
      if (error.code !== 'ENOENT') throw error;
    }
  }
  assert(found, `CLDR no tiene ${packageName}/${fileName} para ${locale}.`);
  return merged;
}

async function loadTranslations(cldrRoot, displayCultures, defaultCulture) {
  assert(displayCultures && typeof displayCultures === 'object' && Object.keys(displayCultures).length > 0, 'displayCultures debe declarar culturas de visualización.');
  const result = [];
  for (const [culture, sourceLocale] of Object.entries(displayCultures).sort(([left], [right]) =>
    Number(right === defaultCulture) - Number(left === defaultCulture) || left.localeCompare(right, 'en'))) {
    const [currencies, countries, timeZones, localePattern] = await Promise.all([
      cldrLocale(cldrRoot, 'cldr-numbers-full', sourceLocale, 'currencies.json', value => value.numbers.currencies),
      cldrLocale(cldrRoot, 'cldr-localenames-full', sourceLocale, 'territories.json', value => value.localeDisplayNames.territories),
      cldrLocale(cldrRoot, 'cldr-dates-full', sourceLocale, 'timeZoneNames.json', value => value.dates.timeZoneNames.zone),
      cldrLocale(cldrRoot, 'cldr-localenames-full', sourceLocale, 'localeDisplayNames.json', value => value.localeDisplayNames.localeDisplayPattern)
    ]);
    result.push({ culture, currencies, countries, timeZones, localePattern: localePattern.localePattern });
  }
  return result;
}

function currencyTranslations(code, officialName, locales) {
  let complete = true;
  const translations = locales.map(locale => {
    const data = locale.currencies[code];
    if (!data?.displayName) complete = false;
    return {
      Culture: locale.culture,
      Name: data?.displayName ?? officialName,
      NamePlural: data?.['displayName-count-other'] ?? data?.displayName ?? officialName,
      DisplaySymbol: data?.symbol ?? code
    };
  });
  return { translations, complete };
}

function countryTranslations(code, locales) {
  return locales.map(locale => {
    const name = locale.countries[code];
    assert(typeof name === 'string', `CLDR no tiene nombre de país ${code} en ${locale.culture}.`);
    return { Culture: locale.culture, Name: name };
  });
}

function timeZoneTranslations(zone, locales, cityOverrides) {
  const parts = zone.Id.split('/');
  const finalPart = parts.at(-1);
  return locales.map(locale => {
    const cldrCity = parts.reduce((entry, part) => entry?.[part], locale.timeZones)?.exemplarCity;
    const overrides = cityOverrides[locale.culture.split('-')[0]];
    const city = overrides?.[zone.Id] ?? overrides?.[finalPart] ?? cldrCity ?? finalPart.replaceAll('_', ' ');
    assert(typeof city === 'string' && city.trim(), `Falta ciudad de ${zone.Id} en ${locale.culture}.`);
    return { Culture: locale.culture, City: city };
  });
}

function cultureTranslations(row, locales) {
  return locales.map(locale => {
    const language = new Intl.DisplayNames([locale.culture], { type: 'language' }).of(row.LanguageCode);
    const region = new Intl.DisplayNames([locale.culture], { type: 'region' }).of(row.CountryCode);
    assert(language && language !== row.LanguageCode && region && region !== row.CountryCode && locale.localePattern,
      `Falta traducción CLDR de cultura ${row.Code} en ${locale.culture}.`);
    const name = locale.localePattern.replace('{0}', language).replace('{1}', region);
    return { DisplayCulture: locale.culture, Name: name[0].toLocaleUpperCase(locale.culture) + name.slice(1) };
  });
}

function validatedCultureSource(document, countrySet, displayCultures, defaultCulture) {
  const rows = document?.Cultures;
  assert(Array.isArray(rows) && rows.length > 0, 'cultures.source.json debe contener Cultures.');
  const codes = new Set();
  for (const row of rows) {
    assert(typeof row.Code === 'string' && row.Code === `${row.LanguageCode}-${row.CountryCode}` && countrySet.has(row.CountryCode),
      `Cultura ${row.Code} tiene código o FK de país inválidos.`);
    assert(!codes.has(row.Code), `Cultura duplicada: ${row.Code}`);
    codes.add(row.Code);
    for (const key of ['DatePattern', 'TimePattern', 'DateTimePattern', 'LongDatePattern', 'DecimalSeparator', 'GroupSeparator', 'CurrencyPattern', 'PercentPattern']) {
      assert(typeof row[key] === 'string' && row[key], `${row.Code}.${key} falta en cultures.source.json.`);
    }
  }
  assert(codes.has(defaultCulture), `Cultura predeterminada desconocida: ${defaultCulture}`);
  for (const code of Object.keys(displayCultures)) assert(codes.has(code), `Cultura habilitada sin patrones: ${code}`);
  for (const row of rows) assert(row.FallbackCulture == null || codes.has(row.FallbackCulture), `${row.Code} tiene FallbackCulture sin FK.`);
  return rows;
}

function validatedTaxIdSource(document, countrySet, displayCultures) {
  const rows = document?.TaxIdTypes;
  assert(Array.isArray(rows), 'tax-id-types.source.json debe contener TaxIdTypes.');
  const codes = new Set();
  for (const row of rows) {
    assert(typeof row.Code === 'string' && row.Code.startsWith(`${row.CountryCode}-`) && countrySet.has(row.CountryCode),
      `${row.Code} tiene FK de país inválida en tax-id-types.source.json.`);
    assert(!codes.has(row.Code), `Tipo fiscal duplicado: ${row.Code}`);
    codes.add(row.Code);
    for (const key of ['Label', 'Mask', 'ValidatorKey']) {
      assert(typeof row[key] === 'string' && row[key], `${row.Code}.${key} falta en tax-id-types.source.json.`);
    }
    assert(['Person', 'Company', 'Both'].includes(row.AppliesTo), `${row.Code}.AppliesTo es inválido.`);
    for (const culture of Object.keys(displayCultures)) {
      assert(typeof row.Names?.[culture] === 'string' && row.Names[culture].trim(),
        `${row.Code} no tiene traducción para ${culture} en tax-id-types.source.json.`);
    }
  }
  return rows;
}

async function checkPinnedPackages(lock) {
  const manifest = await readJson(join(scriptRoot, 'package.json'));
  const installation = await readJson(join(scriptRoot, 'package-lock.json'));
  const expected = {
    'cldr-core': lock.packages?.cldr,
    'cldr-dates-full': lock.packages?.cldrDates,
    'cldr-localenames-full': lock.packages?.cldr,
    'cldr-numbers-full': lock.packages?.cldr,
    'libphonenumber-js': lock.packages?.libphonenumber,
    'fast-xml-parser': lock.packages?.xmlParser
  };
  const packageSources = {};
  for (const [name, version] of Object.entries(expected)) {
    assert(version && manifest.dependencies[name] === version, `Versión de ${name} no coincide entre sources.lock.json y package.json.`);
    const record = installation.packages[`node_modules/${name}`];
    const installed = await readJson(join(scriptRoot, 'node_modules', name, 'package.json'));
    assert(record?.version === version && installed.version === version && record.resolved && record.integrity,
      `Versión instalada o URL/integridad de ${name} no coincide con package-lock.json.`);
    packageSources[name] = { Version: version, Url: record.resolved, Integrity: record.integrity };
  }
  const nodeVersion = (await readFile(join(scriptRoot, '.node-version'), 'utf8')).trim();
  assert(process.version === `v${nodeVersion}`, `Se requiere Node ${nodeVersion}; actual ${process.version}.`);
  return packageSources;
}

async function refreshSnapshots(root, lock, fetchImpl) {
  const next = structuredClone(lock);
  const downloaded = [];
  for (const [name, source] of Object.entries(next.sources)) {
    const path = sourcePath(root, source);
    const response = await fetchImpl(source.url, { headers: { 'User-Agent': 'ArquitecturaBaseMultitenant-ReferenceData/1' } });
    assert(response.ok, `No se pudo descargar ${source.url}: HTTP ${response.status}.`);
    const bytes = Buffer.from(await response.arrayBuffer());
    assert(bytes.length > 0, `Fuente vacía: ${source.url}.`);
    source.sha256 = sha256(bytes);
    downloaded.push({ name, path, bytes });
  }
  const six = downloaded.find(item => item.name === 'sixIso4217');
  const validity = downloaded.find(item => item.name === 'cldrRegionValidity');
  const iana = downloaded.find(item => item.name === 'ianaZone1970');
  assert(six && validity && iana, 'Faltan SIX, validez de regiones CLDR o IANA en sources.lock.json.');
  next.asOf = parseIso(six.bytes.toString('utf8')).version;
  next.sources.sixIso4217.version = next.asOf;
  parseRegularRegions(validity.bytes.toString('utf8'));
  parseIanaZones(iana.bytes.toString('utf8'));
  return {
    lock: next,
    sourceTexts: new Map(downloaded.map(item => [item.name, item.bytes.toString('utf8')])),
    pendingWrites: downloaded
  };
}

export async function generateReferenceData({
  root = scriptRoot,
  outputDir = outputRoot,
  cldrRoot = join(root, 'node_modules'),
  callingCodeForCountry = code => {
    try { return getCountryCallingCode(code); } catch { return null; }
  },
  fetchImpl = globalThis.fetch,
  refresh = false
} = {}) {
  let lock = await readJson(join(root, 'sources.lock.json'));
  const packageSources = await checkPinnedPackages(lock);
  const refreshed = refresh ? await refreshSnapshots(root, lock, fetchImpl) : null;
  if (refreshed) lock = refreshed.lock;
  const [sixXml, validityXml, ianaTab, mappingsDocument, currencyDocument, config, culturesDocument, taxIdTypesDocument] = await Promise.all([
    refreshed ? refreshed.sourceTexts.get('sixIso4217') : verifiedSource(root, lock.sources.sixIso4217),
    refreshed ? refreshed.sourceTexts.get('cldrRegionValidity') : verifiedSource(root, lock.sources.cldrRegionValidity),
    refreshed ? refreshed.sourceTexts.get('ianaZone1970') : verifiedSource(root, lock.sources.ianaZone1970),
    readJson(join(cldrRoot, 'cldr-core/supplemental/codeMappings.json')),
    readJson(join(cldrRoot, 'cldr-core/supplemental/currencyData.json')),
    readJson(join(root, 'habilitados.json')),
    readJson(join(root, 'cultures.source.json')),
    readJson(join(root, 'tax-id-types.source.json'))
  ]);
  const six = parseIso(sixXml);
  assert(six.version === lock.sources.sixIso4217.version && six.version === lock.asOf, 'La fecha de SIX no coincide con sources.lock.json.');
  const regular = parseRegularRegions(validityXml);
  const mappings = mappingsDocument.supplemental.codeMappings;
  const currencyRegions = currencyDocument.supplemental.currencyData.region;
  const countryCodes = [...regular].filter(code => {
    const entry = mappings[code];
    return /^[A-Z]{3}$/.test(entry?._alpha3 ?? '') && /^[0-9]{3}$/.test(entry?._numeric ?? '') && Number(entry._numeric) < 900;
  }).sort();
  assert(countryCodes.length > 0, 'No se encontraron países ISO 3166 vigentes en CLDR.');
  const countryCurrency = new Map();
  for (const code of countryCodes) countryCurrency.set(code, currentTenderCurrency(currencyRegions[code], lock.asOf));
  for (const [country, code] of countryCurrency) {
    if (code === null) continue;
    const source = six.byCode.get(code);
    assert(source, `${code} es referencia activa de CLDR pero falta en SIX List One (FK rota del país ${country}).`);
    assert(Number.isInteger(source.MinorUnits) && source.MinorUnits >= 0, `${code} carece de MinorUnits/unidades menores en SIX.`);
    assert(/^[0-9]{3}$/.test(source.NumericCode ?? ''), `${code} carece de NumericCode ISO válido en SIX.`);
  }
  const currencyCodes = [...six.byCode.values()]
    .filter(value => /^[0-9]{3}$/.test(value.NumericCode ?? '') && Number.isInteger(value.MinorUnits) && value.MinorUnits >= 0)
    .map(value => value.Code).sort();
  const currencySet = new Set(currencyCodes);
  const countrySet = new Set(countryCodes);
  const ianaZones = parseIanaZones(ianaTab, countrySet);
  const zonesByCountry = new Map();
  for (const zone of ianaZones) {
    for (const code of zone.CountryCodes) {
      if (!zonesByCountry.has(code)) zonesByCountry.set(code, zone.Id);
    }
  }
  const currencyOverrides = validateOverrideMap(config, 'currencies', currencySet);
  const countryOverrides = validateOverrideMap(config, 'countries', countrySet);
  const timeZoneOverrides = validateOverrideMap(config, 'timeZones', new Set(ianaZones.map(value => value.Id)));
  assert(currencySet.has(config.defaults?.currency), `Moneda predeterminada desconocida: ${config.defaults?.currency}`);
  assert(countrySet.has(config.defaults?.country), `País predeterminado desconocido: ${config.defaults?.country}`);
  const defaultZone = ianaZones.find(value => value.Id === config.defaults?.timeZone);
  assert(defaultZone?.CountryCodes.includes(config.defaults.country),
    `${config.defaults?.timeZone} no pertenece al país predeterminado ${config.defaults?.country}.`);
  zonesByCountry.set(config.defaults.country, defaultZone.Id);
  const locales = await loadTranslations(cldrRoot, config.displayCultures, config.defaults.culture);
  assert(locales.some(locale => locale.culture === config.defaults.culture), `Cultura predeterminada desconocida: ${config.defaults.culture}`);
  const cultureRows = validatedCultureSource(culturesDocument, countrySet, config.displayCultures, config.defaults.culture);
  const taxIdRows = validatedTaxIdSource(taxIdTypesDocument, countrySet, config.displayCultures);
  const cultureOverrides = validateOverrideMap(config, 'cultures', new Set(cultureRows.map(value => value.Code)));
  const taxIdOverrides = validateOverrideMap(config, 'taxIdTypes', new Set(taxIdRows.map(value => value.Code)));
  const cityOverrides = {};
  for (const language of new Set(locales.map(locale => locale.culture.split('-')[0]))) {
    const overrides = await readJson(join(root, `ciudades.${language}.json`));
    assert(overrides && typeof overrides === 'object' && !Array.isArray(overrides), `ciudades.${language}.json debe ser un objeto.`);
    for (const [key, value] of Object.entries(overrides)) {
      assert(typeof value === 'string' && value.trim(), `ciudades.${language}.json tiene traducción vacía para ${key}.`);
    }
    cityOverrides[language] = overrides;
  }
  const currencies = currencyCodes.map(code => {
    const source = six.byCode.get(code);
    const { translations, complete } = currencyTranslations(code, source.Name, locales);
    const selected = selection(code, config.defaults.currency, currencyOverrides);
    assert(complete || currencyOverrides[code]?.isEnabled !== true, `${code} no tiene traducción CLDR y no puede habilitarse.`);
    return {
      Code: code,
      NumericCode: source.NumericCode,
      MinorUnits: source.MinorUnits,
      Symbol: translations.find(value => value.Culture === config.defaults.culture).DisplaySymbol,
      IsEnabled: selected.IsEnabled && complete,
      SortOrder: selected.SortOrder,
      Translations: translations
    };
  });
  const countries = countryCodes.map(code => {
    const currency = countryCurrency.get(code);
    const callingCode = callingCodeForCountry(code);
    const timeZone = zonesByCountry.get(code) ?? null;
    assert(currency === null || currencySet.has(currency), `${currency} es referencia activa de CLDR pero falta en SIX List One (FK rota del país ${code}).`);
    assert(callingCode === null || /^[0-9]+$/.test(String(callingCode)), `Prefijo telefónico inválido para ${code}.`);
    const selected = selection(code, config.defaults.country, countryOverrides);
    return {
      Code: code,
      Alpha3: mappings[code]._alpha3,
      NumericCode: mappings[code]._numeric,
      CallingCode: callingCode === null ? null : String(callingCode),
      DefaultCurrencyCode: currency,
      DefaultTimeZoneId: timeZone,
      IsEnabled: selected.IsEnabled && currency !== null && callingCode !== null && timeZone !== null,
      SortOrder: selected.SortOrder,
      Translations: countryTranslations(code, locales)
    };
  });
  const enabledCountries = new Set(countries.filter(value => value.IsEnabled).map(value => value.Code));
  const timeZones = ianaZones.map(zone => {
    const selected = selection(zone.Id, zone.Id === 'UTC' || zone.CountryCodes.some(code => enabledCountries.has(code)) ? zone.Id : null, timeZoneOverrides);
    return {
      Id: zone.Id,
      CountryCodes: zone.CountryCodes,
      IsEnabled: selected.IsEnabled,
      SortOrder: selected.SortOrder,
      Translations: timeZoneTranslations(zone, locales, cityOverrides)
    };
  });
  const enabledCurrenciesByCode = new Map(currencies.map(value => [value.Code, value.IsEnabled]));
  const enabledTimeZonesById = new Map(timeZones.map(value => [value.Id, value.IsEnabled]));
  for (const country of countries.filter(value => value.IsEnabled)) {
    assert(enabledCurrenciesByCode.get(country.DefaultCurrencyCode),
      `${country.Code} tiene moneda predeterminada ${country.DefaultCurrencyCode} deshabilitada.`);
    assert(enabledTimeZonesById.get(country.DefaultTimeZoneId),
      `${country.Code} tiene zona predeterminada ${country.DefaultTimeZoneId} deshabilitada.`);
  }
  const cultures = cultureRows.map(row => {
    const hasTranslations = Object.hasOwn(config.displayCultures, row.Code);
    const selected = selection(row.Code, hasTranslations ? row.Code : null, cultureOverrides);
    assert(!selected.IsEnabled || hasTranslations, `${row.Code} no figura en displayCultures y carece de traducciones.`);
    return {
      ...row,
      IsEnabled: selected.IsEnabled,
      IsDefault: row.Code === config.defaults.culture,
      SortOrder: selected.SortOrder,
      Translations: cultureTranslations(row, locales)
    };
  });
  assert(cultures.filter(value => value.IsDefault && value.IsEnabled).length === 1, 'Debe haber una sola cultura predeterminada habilitada.');
  const taxIdTypes = taxIdRows.map(row => {
    const selected = selection(row.Code, enabledCountries.has(row.CountryCode) ? row.Code : null, taxIdOverrides);
    const { Names, ...source } = row;
    return {
      ...source,
      IsEnabled: selected.IsEnabled && enabledCountries.has(row.CountryCode),
      SortOrder: selected.SortOrder,
      Translations: locales.map(locale => ({ Culture: locale.culture, Name: Names[locale.culture] }))
    };
  });
  const sources = { AsOf: lock.asOf, SIX: lock.sources.sixIso4217, CLDR: lock.sources.cldrRegionValidity, IANA: lock.sources.ianaZone1970, Packages: packageSources };
  const currencyJson = stableJson({ Sources: sources, Currencies: currencies });
  const countryJson = stableJson({ Sources: sources, Countries: countries });
  const timeZoneJson = stableJson({ Sources: sources, TimeZones: timeZones });
  const cultureJson = stableJson({ Sources: sources, Cultures: cultures });
  const taxIdTypeJson = stableJson({ Sources: sources, TaxIdTypes: taxIdTypes });
  if (refreshed) {
    for (const item of refreshed.pendingWrites) {
      await mkdir(dirname(item.path), { recursive: true });
      await writeFile(item.path, item.bytes);
    }
    await writeFile(join(root, 'sources.lock.json'), stableJson(lock));
  }
  await mkdir(outputDir, { recursive: true });
  await Promise.all([
    writeFile(join(outputDir, 'currencies.json'), currencyJson),
    writeFile(join(outputDir, 'countries.json'), countryJson),
    writeFile(join(outputDir, 'time-zones.json'), timeZoneJson),
    writeFile(join(outputDir, 'cultures.json'), cultureJson),
    writeFile(join(outputDir, 'tax-id-types.json'), taxIdTypeJson)
  ]);
  return { currencies, countries, timeZones, cultures, taxIdTypes };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const flags = process.argv.slice(2);
  assert(flags.length === 0 || (flags.length === 1 && flags[0] === '--refresh'), 'Uso: node generar.mjs [--refresh]');
  await generateReferenceData({ refresh: flags[0] === '--refresh' });
}
