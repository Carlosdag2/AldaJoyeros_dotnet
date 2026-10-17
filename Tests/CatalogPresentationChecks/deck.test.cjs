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
for (const size of [0, 1, 2, 17, 28000]) {
    const deck = new CatalogDeck(Array.from({ length: size }, (_, id) => ({ id })));
    const seen = new Set();
    for (let i = 0; i < size; i++) seen.add(deck.next(false).id);
    assert.equal(seen.size, size);
    assert.equal(deck.next(false), null, 'Debe detenerse al terminar la selección');
    assert.equal(deck.next(false), null, 'No debe empezar otra vuelta por precarga');
    if (!size) continue;
    const last = deck.lastId;
    const first = deck.next(true);
    if (size > 1) assert.notEqual(first.id, last, 'Activar el bucle no debe repetir el último producto');
    assert.equal(deck.round, 2);
}
console.log('PASS: una vuelta finita y activación posterior del bucle, incluido un único producto.');
