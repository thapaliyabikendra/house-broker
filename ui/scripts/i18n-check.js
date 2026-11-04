const fs = require('fs');
const paths = ['public/assets/i18n/en.json', 'public/assets/i18n/ne.json'];
const [enRaw, neRaw] = paths.map((p) => JSON.parse(fs.readFileSync(p, 'utf8')));

function flatten(obj, prefix = '') {
  return Object.keys(obj).reduce((acc, key) => {
    const next = prefix ? `${prefix}.${key}` : key;
    if (typeof obj[key] === 'object' && obj[key] !== null) {
      return { ...acc, ...flatten(obj[key], next) };
    }
    return { ...acc, [next]: obj[key] };
  }, {});
}

const enKeys = Object.keys(flatten(enRaw)).sort();
const neKeys = Object.keys(flatten(neRaw)).sort();
const missingInNe = enKeys.filter((k) => !neKeys.includes(k));
const missingInEn = neKeys.filter((k) => !enKeys.includes(k));

if (missingInNe.length || missingInEn.length) {
  console.error('i18n key parity failed');
  if (missingInNe.length) console.error('Missing in ne:', missingInNe);
  if (missingInEn.length) console.error('Missing in en:', missingInEn);
  process.exit(1);
}
console.log('i18n key parity OK');
