(() => {
    const states = new WeakMap();
    const number = new Intl.NumberFormat('es-ES', { maximumFractionDigits: 0, useGrouping: 'always' });
    const money = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', useGrouping: 'always' });
    function scale(values, integer) {
        const maximum = Math.max(0, ...values);
        if (!maximum) return { maximum: integer ? 4 : 1, step: integer ? 1 : .25 };
        const raw = Math.max(maximum / 4, integer ? .25 : .01), power = 10 ** Math.floor(Math.log10(raw));
        let step = [1, 2, 2.5, 5, 10].find(v => v * power >= raw) * power;
        if (integer) step = Math.max(1, Math.ceil(step));
        else step = Math.ceil(step * 100 - 1e-8) / 100;
        return { maximum: step * 4, step };
    }
    function select(root, index) {
        const state = states.get(root); if (!state) return;
        index = Math.max(0, Math.min(state.points.length - 1, Math.round(index)));
        state.index = index;
        const point = state.points[index], x = 8 + index * 584 / Math.max(1, state.points.length - 1), y = 188 - point.Value / state.maximum * 176;
        root.querySelector('[data-chart-selector]').value = index;
        root.querySelector('[data-chart-label]').textContent = point.Label;
        root.querySelector('[data-chart-value]').textContent = root.dataset.chartUnit === 'currency' ? money.format(point.Value) : number.format(point.Value);
        root.querySelector('[data-chart-selector]').setAttribute('aria-valuetext', point.Label + ': ' + root.querySelector('[data-chart-value]').textContent);
        root.querySelector('[data-chart-cursor]').setAttribute('x1', x); root.querySelector('[data-chart-cursor]').setAttribute('x2', x);
        root.querySelector('[data-chart-dot]').setAttribute('cx', x); root.querySelector('[data-chart-dot]').setAttribute('cy', y);
    }
    function update(root, points) {
        points = points.map(p => ({ Label: String(p.Label ?? p.label ?? ''), AxisLabel: String(p.AxisLabel ?? p.axisLabel ?? ''), Value: Math.max(0, Number(p.Value ?? p.value) || 0) }));
        if (!points.length) points = [{ Label: 'Sin datos', AxisLabel: 'Sin datos', Value: 0 }];
        const previous = states.get(root), oldLabel = previous?.points[previous.index]?.Label;
        const followLast = !previous || previous.index === previous.points.length - 1;
        const axis = scale(points.map(p => p.Value), root.dataset.chartUnit === 'integer');
        const matchingIndex = points.findIndex(p => p.Label === oldLabel);
        const index = followLast ? points.length - 1 : matchingIndex >= 0 ? matchingIndex : Math.min(previous.index, points.length - 1);
        states.set(root, { points, index, maximum: axis.maximum }); root.dataset.chartCeiling = axis.maximum;
        for (let i = 0; i < 5; i++) {
            const tick = root.querySelector(`[data-chart-tick="${i}"]`), value = axis.step * (4 - i);
            tick.textContent = root.dataset.chartUnit === 'currency' ? new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', useGrouping: 'always', maximumFractionDigits: axis.step % 1 !== 0 ? 2 : 0, minimumFractionDigits: axis.step % 1 !== 0 ? 2 : 0 }).format(value) : number.format(value);
            tick.title = root.dataset.chartUnit === 'currency' ? money.format(value) : number.format(value);
        }
        root.querySelector('[data-chart-line]').setAttribute('points', points.map((p, i) => `${8 + i * 584 / Math.max(1, points.length - 1)},${188 - p.Value / axis.maximum * 176}`).join(' '));
        root.querySelector('[data-chart-first]').textContent = points[0].AxisLabel;
        root.querySelector('[data-chart-middle]').textContent = points[Math.floor(points.length / 2)].AxisLabel;
        root.querySelector('[data-chart-last]').textContent = points[points.length - 1].AxisLabel;
        const selector = root.querySelector('[data-chart-selector]'); selector.max = points.length - 1; selector.disabled = points.length < 2;
        select(root, index);
    }
    for (const root of document.querySelectorAll('[data-number-chart]')) {
        update(root, JSON.parse(root.querySelector('[data-chart-data]').textContent));
        root.querySelector('[data-chart-selector]').addEventListener('input', e => select(root, Number(e.target.value)));
        const choose = event => { const bounds = event.currentTarget.getBoundingClientRect(); select(root, (((event.clientX - bounds.left) / bounds.width * 600 - 8) / 584) * (states.get(root).points.length - 1)); };
        root.querySelector('[data-chart-plot]').addEventListener('pointermove', choose);
        root.querySelector('[data-chart-plot]').addEventListener('pointerdown', choose);
    }
    window.aldaMetricCharts = { update };
})();
