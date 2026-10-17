(function (root) {
    'use strict';
    class CatalogDeck {
        constructor(items, random = Math.random) {
            this.items = [...new Map(items.map(item => [item.id, item])).values()];
            this.random = random; this.order = []; this.position = 0; this.round = 0; this.lastId = null;
        }
        next() {
            if (!this.items.length) return null;
            if (this.position >= this.order.length) {
                this.order = [...this.items];
                for (let i = this.order.length - 1; i > 0; i--) {
                    const j = Math.floor(this.random() * (i + 1));
                    [this.order[i], this.order[j]] = [this.order[j], this.order[i]];
                }
                if (this.order.length > 1 && this.order[0].id === this.lastId)
                    [this.order[0], this.order[1]] = [this.order[1], this.order[0]];
                this.position = 0; this.round++;
            }
            const item = this.order[this.position++]; this.lastId = item.id; return item;
        }
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = CatalogDeck;
    else root.CatalogDeck = CatalogDeck;
})(typeof window !== 'undefined' ? window : globalThis);
