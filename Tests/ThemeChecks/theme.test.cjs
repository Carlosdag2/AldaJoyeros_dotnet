const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync(require.resolve('../../wwwroot/js/site.js'), 'utf8');

function browser({ saved, legacy, dark = false, contrast = false, blocked = false } = {}) {
    const storage = new Map();
    if (saved) storage.set('alda-theme-preference', saved);
    if (legacy) storage.set('hc-theme-preference', legacy);
    const root = { dataset: {}, style: {}, classList: { toggle() {} } };
    const events = new Map();
    const media = new Map();
    const window = {
        matchMedia(query) {
            const result = { matches: query.includes('color-scheme') ? dark : contrast,
                addEventListener(_, fn) { this.change = fn; } };
            media.set(query, result);
            return result;
        },
        dispatchEvent() {}, addEventListener(name, fn) { events.set(name, fn); }
    };
    const document = { documentElement: root, querySelectorAll: () => [], addEventListener() {} };
    vm.runInNewContext(source, { window, document, CustomEvent: class {}, localStorage: {
        getItem(key) { if (blocked) throw Error('Blocked'); return storage.get(key) ?? null; },
        setItem(key, value) { if (blocked) throw Error('Blocked'); storage.set(key, value); }
    } });
    return { api: window.AldaTheme, root, storage,
        system(darkValue, contrastValue = false) {
            media.get('(prefers-color-scheme: dark)').matches = darkValue;
            media.get('(prefers-contrast: more)').matches = contrastValue;
            media.get('(prefers-color-scheme: dark)').change();
        }, fromTab(value) { events.get('storage')({ key: 'alda-theme-preference', newValue: value }); }
    };
}
const automatic = browser({ dark: true });
assert.equal(automatic.root.dataset.theme, 'dark', 'Aplica el tema antes de cargar el cuerpo');
assert.equal(automatic.storage.size, 0, 'No fija una preferencia cuando sigue el sistema');
automatic.system(false); assert.equal(automatic.api.getTheme(), 'light');
automatic.system(true, true); assert.equal(automatic.api.getTheme(), 'high-contrast');
automatic.api.setPreference('dark'); automatic.system(false, true);
assert.equal(automatic.api.getTheme(), 'dark', 'La decisión manual prevalece sobre el sistema');
assert.equal(automatic.storage.get('alda-theme-preference'), 'dark');
automatic.api.setPreference('invalid'); assert.equal(automatic.api.getPreference(), 'dark');
automatic.api.setPreference('system'); assert.equal(automatic.api.getTheme(), 'high-contrast');
automatic.fromTab('light'); assert.equal(automatic.api.getTheme(), 'light');
automatic.fromTab(null); assert.equal(automatic.api.getPreference(), 'system');
assert.equal(browser({ saved: 'light', dark: true }).api.getTheme(), 'light');
assert.equal(browser({ legacy: 'normal', dark: true }).api.getTheme(), 'light');
assert.equal(browser({ legacy: 'high-contrast' }).api.getTheme(), 'high-contrast');
assert.equal(browser({ saved: 'dark', legacy: 'normal' }).api.getTheme(), 'dark');
assert.equal(browser({ saved: 'invalid', dark: true }).api.getTheme(), 'dark');
const privateMode = browser({ blocked: true, dark: true });
privateMode.api.setPreference('light'); assert.equal(privateMode.api.getTheme(), 'light');
console.log('PASS: tema inicial, sistema, elección manual, persistencia, preferencias anteriores, otras pestañas y almacenamiento bloqueado.');
