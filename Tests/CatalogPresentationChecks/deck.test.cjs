const assert = require('node:assert/strict');
const CatalogDeck = require('../../wwwroot/js/catalog-deck.js');
for (const size of [0, 1, 2, 3, 500, 28000]) {
    const products = Array.from({ length: size }, (_, id) => ({ id }));
    const deck = new CatalogDeck(products);
    if (!size) { assert.equal(deck.next(), null); continue; }
    let last;
    for (let round = 0; round < 5; round++) {
        const seen = new Set();
        for (let i = 0; i < size; i++) {
            const item = deck.next();
            assert.ok(!seen.has(item.id), 'Producto repetido dentro de la vuelta');
            if (i === 0 && size > 1) assert.notEqual(item.id, last, 'Repetición al cambiar de vuelta');
            seen.add(item.id); last = item.id;
        }
        assert.equal(seen.size, size); assert.equal(deck.round, round + 1);
    }
}
const duplicates = new CatalogDeck([{ id: 1 }, { id: 1 }, { id: 2 }]);
assert.equal(duplicates.items.length, 2);
const repeatBoundary = new CatalogDeck([{ id: 1 }, { id: 2 }], () => 0);
assert.deepEqual(Array.from({ length: 8 }, () => repeatBoundary.next().id), [2, 1, 2, 1, 2, 1, 2, 1]);
console.log('PASS: vueltas completas, sin duplicados, límites entre vueltas, catálogo vacío, único y 28.000 productos.');
