const handlers = new WeakMap();
const fits = new WeakMap();
const NS = 'http://www.w3.org/2000/svg';
let described = 0;
// How far each key moves along a series, or along the readout's X values: one, ten, or to either end.
const steps = { ArrowLeft: -1, ArrowRight: 1, PageUp: -10, PageDown: 10, Home: -Infinity, End: Infinity };

export function attach(root, dotnet) {
    const tooltip = document.createElement('div');
    tooltip.className = 'lumen-tooltip';
    tooltip.setAttribute('aria-hidden', 'true');
    tooltip.hidden = true;
    root.appendChild(tooltip);
    // The readout the component worked out for the drawing shown, the X it stands at, its guide and rings, the mark that is the chart's
    // one tab stop, the series the keyboard keeps to, and the drawing they belong to; and the plots a drag zooms across, the drag under
    // way, and whether the click a drag ends with is to be ignored.
    const state = { readout: null, index: new Map(), column: -1, overlay: null, current: null, series: null, keep: false, svg: null, timer: 0,
        plot: null, brush: null, suppress: false };

    const mark = target => {
        const found = target instanceof Element ? target.closest('.lumen-datum') : null;
        return found && root.contains(found) ? found : null;
    };
    const drawing = () => root.querySelector(':scope > .lumen-viewport > svg');
    const marks = () => [...root.querySelectorAll(':scope > .lumen-viewport .lumen-datum')];
    // A mark is known by its series and point, or, for an aggregate such as a reference or a box, by its place among the aggregates.
    const key = element => element.dataset.series !== undefined ? `${element.dataset.series}:${element.dataset.point}`
        : 'a' + marks().filter(m => m.dataset.series === undefined).indexOf(element);
    const clear = () => { state.column = -1; state.overlay?.replaceChildren(); };
    const hide = () => { tooltip.hidden = true; clear(); };
    const plain = () => { tooltip.classList.remove('lumen-readout-tip'); };
    const place = (element, event) => {
        const bounds = root.getBoundingClientRect();
        const box = element.getBoundingClientRect();
        const x = event && event.clientX ? event.clientX : box.left + box.width / 2;
        const y = event && event.clientY ? Math.min(event.clientY, box.top + box.height / 2) : box.top;
        // A sparkline is too small to hold its tooltip: it stands just above the drawing rather than over the line, as wide as its
        // words need, and is moved in from the page's sides by the 16 pixels a page keeps clear, so it is never cut.
        if (root.classList.contains('lumen-spark')) {
            const half = tooltip.offsetWidth / 2, page = document.documentElement.clientWidth;
            tooltip.style.left = Math.min(Math.max(x, half + 16), page - half - 16) - bounds.left + 'px';
            tooltip.style.top = '-6px';
            return;
        }
        tooltip.style.left = Math.min(Math.max(x - bounds.left, 60), bounds.width - 60) + 'px';
        tooltip.style.top = Math.max(y - bounds.top - 12, 26) + 'px';
    };
    const show = event => {
        const element = mark(event.target);
        if (!element) { hide(); return; }
        clear();
        plain();
        tooltip.textContent = element.getAttribute('aria-label') || '';
        tooltip.hidden = false;
        place(element, event.clientX ? event : null);
    };

    // The shared readout at one X: a guide through every pane, a ring round each series' point there, and one tooltip beside the guide
    // that reads the X and then each series, in legend order. The guide is the muted text colour and each ring is outlined in the text
    // colour, both of which clear contrast on the chart's background, round a dot in the series' colour.
    const draw = (name, attributes) => {
        const element = document.createElementNS(NS, name);
        for (const [attribute, value] of Object.entries(attributes)) element.setAttribute(attribute, value);
        return element;
    };
    const showReadout = column => {
        const readout = state.readout, svg = drawing();
        if (!readout || !svg || column < 0 || column >= readout.columns.length) return;
        state.column = column;
        const [x, label, entries] = readout.columns[column];
        if (!state.overlay || state.overlay.ownerSVGElement !== svg) {
            state.overlay = draw('g', { class: 'lumen-readout', 'aria-hidden': 'true', 'pointer-events': 'none' });
            svg.appendChild(state.overlay);
        }
        // A drawing left unpainted carries the colour it stands on, which its halos are drawn in, as --lumen-ground.
        const ground = svg.style.getPropertyValue('--lumen-ground').trim() || getComputedStyle(svg).backgroundColor;
        const parts = [draw('line', { x1: x, x2: x, y1: readout.top, y2: readout.bottom, 'stroke-width': 1, 'vector-effect': 'non-scaling-stroke', style: 'stroke:var(--lumen-muted)' })];
        for (const [, , y, , color] of entries) {
            if (y === null) continue;
            parts.push(draw('circle', { cx: x, cy: y, r: 7, fill: 'none', stroke: 'currentColor', 'stroke-width': 1.5, 'vector-effect': 'non-scaling-stroke' }),
                draw('circle', { cx: x, cy: y, r: 4, fill: color, stroke: ground, 'stroke-width': 1.5, 'vector-effect': 'non-scaling-stroke' }));
        }
        state.overlay.replaceChildren(...parts);
        tooltip.textContent = [label, ...entries.map(entry => entry[3])].join('\n');
        tooltip.classList.add('lumen-readout-tip');
        tooltip.hidden = false;
        // Beside the guide at the top of the plot, on whichever side has room, and never past the chart's sides.
        const bounds = root.getBoundingClientRect(), matrix = svg.getScreenCTM();
        if (!matrix) return;
        const at = new DOMPoint(x, readout.top).matrixTransform(matrix);
        const width = tooltip.offsetWidth, from = at.x - bounds.left;
        let left = from + 12;
        if (left + width > bounds.width - 4) left = from - 12 - width;
        tooltip.style.left = Math.max(4, Math.min(left, bounds.width - width - 4)) + 'px';
        tooltip.style.top = at.y - bounds.top + 'px';
    };
    // The X nearest a position across the drawing.
    const nearest = position => {
        const columns = state.readout.columns;
        let low = 0, high = columns.length - 1;
        while (low < high) { const middle = (low + high) >> 1; if (columns[middle][0] < position) low = middle + 1; else high = middle; }
        return low > 0 && position - columns[low - 1][0] <= columns[low][0] - position ? low - 1 : low;
    };
    const pointed = event => {
        const readout = state.readout, svg = drawing(), matrix = svg?.getScreenCTM();
        if (!readout.columns.length || !matrix) return;
        const at = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
        if (at.x < readout.left - 8 || at.x > readout.right + 8 || at.y < readout.top - 8 || at.y > readout.bottom + 8) { hide(); return; }
        const column = nearest(at.x);
        if (column !== state.column || tooltip.hidden) showReadout(column);
    };
    // The status line reads the readout too, a moment after the keyboard stops on an X, so holding a key down does not flood it.
    const announce = column => {
        clearTimeout(state.timer);
        state.timer = setTimeout(() => dotnet.invokeMethodAsync('Readout', column), 150);
    };

    // One tab stop for every chart: the mark last focused, or else the first point of the first series, takes Tab; the others are reached
    // with the arrow keys. The drawing itself keeps every mark at tabindex 0, so a static page stays readable without this script.
    const rove = current => {
        for (const element of marks()) element.setAttribute('tabindex', element === current ? '0' : '-1');
        state.current = key(current);
    };
    const settle = () => {
        const all = marks();
        if (!all.length) return;
        const points = all.filter(m => m.dataset.series !== undefined);
        const first = points.length ? points.reduce((best, m) => Number(m.dataset.series) < Number(best.dataset.series)
            || m.dataset.series === best.dataset.series && Number(m.dataset.point) < Number(best.dataset.point) ? m : best) : all[0];
        rove(all.find(m => key(m) === state.current) || first);
    };
    // The chart's marks in the rows the arrow keys move along: each series' points in order, then the aggregates — references, bins,
    // boxes — in the order they are drawn.
    const rows = () => {
        const series = new Map(), aggregates = [];
        for (const element of marks()) {
            if (element.dataset.series === undefined) { aggregates.push(element); continue; }
            const s = Number(element.dataset.series);
            if (!series.has(s)) series.set(s, []);
            series.get(s).push(element);
        }
        const result = [...series.keys()].sort((a, b) => a - b).map(s => series.get(s).sort((a, b) => a.dataset.point - b.dataset.point));
        if (aggregates.length) result.push(aggregates);
        return result;
    };
    const centre = element => { const box = element.getBoundingClientRect(); return box.left + box.width / 2; };
    const along = (length, from, step) => step === -Infinity ? 0 : step === Infinity ? length - 1 : Math.max(0, Math.min(length - 1, from + step));
    // Left and Right step along the series, skipping its gaps, which have no mark unless a gap label writes one; Up and Down go to the
    // point nearest the same X in the series before or after.
    const move = (element, keyName) => {
        const all = rows(), row = all.findIndex(r => r.includes(element));
        if (row < 0) return element;
        if (keyName in steps) return all[row][along(all[row].length, all[row].indexOf(element), steps[keyName])];
        const next = all[row + (keyName === 'ArrowUp' ? -1 : 1)];
        if (!next) return element;
        const x = centre(element);
        return next.reduce((best, m) => Math.abs(centre(m) - x) < Math.abs(centre(best) - x) ? m : best);
    };
    const markOf = entry => root.querySelector(`:scope > .lumen-viewport .lumen-datum[data-series="${entry[0]}"][data-point="${entry[1]}"]`);
    // With a shared readout, Left and Right move from one X to the next, staying with the series the reader chose where it has a point
    // there, and Up and Down move between the series at that X.
    const step = (element, keyName) => {
        const columns = state.readout.columns, column = state.index.get(key(element));
        if (!(keyName in steps)) {
            const here = columns[column][2].map(markOf).filter(Boolean), at = here.indexOf(element);
            return here[Math.max(0, Math.min(here.length - 1, at + (keyName === 'ArrowUp' ? -1 : 1)))] ?? element;
        }
        const by = steps[keyName], direction = by === -Infinity ? 1 : by === Infinity ? -1 : Math.sign(by);
        // An X whose points are all thinned out of the drawing has no mark to focus, so the move goes on past it.
        for (let c = along(columns.length, column, by); c >= 0 && c < columns.length; c += direction) {
            const there = columns[c][2].map(markOf).filter(Boolean);
            if (there.length) return there.find(m => Number(m.dataset.series) === state.series) ?? there[0];
        }
        return element;
    };

    const select = event => {
        // A drag across the plots ends with a click, which selects nothing.
        if (event.type === 'click' && state.suppress) { state.suppress = false; return; }
        const point = event.target.closest('[data-point]');
        if (!point || !root.contains(point)) return;
        if (event.type === 'keydown') event.preventDefault();
        dotnet.invokeMethodAsync('SelectPoint', Number(point.dataset.series), Number(point.dataset.point));
    };
    const keydown = event => {
        if (event.key === 'Escape') { hide(); return; }
        const element = mark(event.target);
        if (!element) return;
        if (['Enter', ' '].includes(event.key)) { select(event); return; }
        if (!(event.key in steps) && event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return;
        event.preventDefault();
        const reading = state.readout && state.index.has(key(element));
        const next = reading ? step(element, event.key) : move(element, event.key);
        if (!next || next === element) return;
        // Left and Right keep to the series chosen, even through an X where it has no point.
        state.keep = reading && event.key in steps;
        next.focus();
        state.keep = false;
    };
    const focused = event => {
        const element = mark(event.target);
        if (!element) return;
        rove(element);
        const column = state.readout ? state.index.get(key(element)) : undefined;
        if (column === undefined) { show(event); return; }
        if (!state.keep) state.series = Number(element.dataset.series);
        showReadout(column);
        announce(column);
    };
    // Drag to zoom: a mouse or a pen pressed on the plots and drawn 8 pixels or more across them marks a band through every pane, and
    // letting go zooms the chart to the X the band covers, through the component. A shorter drag is a click; a touch keeps the tap that
    // reads the chart; Escape lets a drag go without zooming. The band is the text colour at a tenth, edged in the muted text colour,
    // which clears 3:1 on the chart's background and on the band.
    const located = (event, svg) => { const matrix = svg?.getScreenCTM(); return matrix ? new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse()) : null; };
    let escaped = null;
    const unbrush = () => {
        const brush = state.brush;
        state.brush = null;
        if (!brush?.band) return;
        brush.band.remove();
        root.classList.remove('lumen-brushing');
        try { root.releasePointerCapture?.(brush.id); } catch { }
        document.removeEventListener('keydown', escaped, true);
        // The click the drag ends with is ignored.
        state.suppress = true;
    };
    escaped = event => {
        if (event.key !== 'Escape' || !state.brush?.band) return;
        event.preventDefault();
        event.stopPropagation();
        unbrush();
    };
    const pressed = event => {
        state.suppress = false;
        const plot = state.plot, svg = drawing();
        if (!plot || !svg || event.pointerType === 'touch' || event.button !== 0) return;
        const at = located(event, svg);
        if (!at || at.x < plot.left || at.x > plot.right || at.y < plot.top || at.y > plot.bottom) return;
        state.brush = { id: event.pointerId, from: at.x, to: at.x, start: event.clientX, svg, band: null };
    };
    // Whether the pointer is dragging a band, which is drawn to where the pointer stands, kept to the plots.
    const brushing = event => {
        const brush = state.brush, plot = state.plot;
        if (!brush || event.pointerId !== brush.id || !plot) return false;
        if (!brush.band) {
            if (Math.abs(event.clientX - brush.start) < 8) return false;
            brush.band = draw('g', { class: 'lumen-brush', 'aria-hidden': 'true', 'pointer-events': 'none' });
            brush.svg.appendChild(brush.band);
            root.classList.add('lumen-brushing');
            window.getSelection?.()?.removeAllRanges();
            try { root.setPointerCapture?.(brush.id); } catch { }
            document.addEventListener('keydown', escaped, true);
            hide();
        }
        const at = located(event, brush.svg);
        if (at) brush.to = Math.max(plot.left, Math.min(plot.right, at.x));
        const from = Math.min(brush.from, brush.to), to = Math.max(brush.from, brush.to);
        const edge = { y1: plot.top, y2: plot.bottom, 'stroke-width': 1.5, 'vector-effect': 'non-scaling-stroke', style: 'stroke:var(--lumen-muted)' };
        brush.band.replaceChildren(draw('rect', { x: from, y: plot.top, width: to - from, height: plot.bottom - plot.top, fill: 'currentColor', 'fill-opacity': .1 }),
            draw('line', { x1: from, x2: from, ...edge }), draw('line', { x1: to, x2: to, ...edge }));
        return true;
    };
    const released = event => {
        const brush = state.brush;
        if (!brush || event.pointerId !== brush.id) return;
        if (!brush.band) { state.brush = null; return; }
        unbrush();
        if (brush.to !== brush.from) dotnet.invokeMethodAsync('ZoomTo', brush.from, brush.to);
    };
    const cancelled = event => { if (state.brush && event.pointerId === state.brush.id) { if (state.brush.band) unbrush(); else state.brush = null; } };

    // While a band is drawn the readout and the tooltips stay hidden.
    const over = event => { if (state.brush?.band) return; if (state.readout) pointed(event); else show(event); };
    const moved = event => { if (brushing(event)) return; if (state.readout) pointed(event); else if (!tooltip.hidden) show(event); };
    const out = event => { if (!state.readout && !state.brush?.band && !mark(event.relatedTarget)) hide(); };
    // A readout stays while the pointer is anywhere on the chart, and after a tap, so a reader on a phone can read it.
    const left = event => { if (state.readout && !state.brush?.band && event.pointerType !== 'touch') hide(); };
    const blurred = event => { if (!mark(event.relatedTarget)) hide(); };

    // The words that name the keys describe the chart's scrolling viewport while it scrolls, and otherwise the drawing, as they do a
    // sparkline's. They are given an ID here, so the server's drawing stays the same for every chart and every render.
    const words = root.querySelector(':scope > .lumen-keys');
    const keys = words ? words.id || (words.id = 'lumen-keys-' + ++described) : null;
    // The viewport is a tab stop, a region named "Scrollable chart", only while the drawing is wider or taller than it and so scrolls,
    // since a keyboard needs it then to scroll; while the drawing fits it is neither, and the chart's one tab stop is its roving mark.
    // The markup is written as a region, so a page without this script keeps the stop; the script settles it on load, for each new
    // drawing and whenever the viewport or its drawing changes size.
    const viewport = root.classList.contains('lumen-spark') ? null : root.querySelector(':scope > .lumen-viewport');
    const region = () => {
        if (!viewport) return;
        const svg = drawing();
        if (viewport.scrollWidth > viewport.clientWidth + 1 || viewport.scrollHeight > viewport.clientHeight + 1) {
            viewport.setAttribute('tabindex', '0');
            viewport.setAttribute('role', 'region');
            viewport.setAttribute('aria-label', 'Scrollable chart');
            if (keys) { viewport.setAttribute('aria-describedby', keys); svg?.removeAttribute('aria-describedby'); }
        } else {
            viewport.setAttribute('tabindex', '-1');
            viewport.removeAttribute('role');
            viewport.removeAttribute('aria-label');
            if (keys) { viewport.removeAttribute('aria-describedby'); svg?.setAttribute('aria-describedby', keys); }
        }
    };
    const sized = viewport ? new ResizeObserver(region) : null;
    if (viewport) { sized.observe(viewport); region(); }

    // Each new drawing: the readout it reads, its one tab stop, and, on a sparkline, the keys named in its drawing's description. A
    // drawing replaced takes its guide and tooltip with it.
    state.drawn = (data, plot) => {
        state.readout = data ? { top: data[0], bottom: data[1], left: data[2], right: data[3], columns: data[4] } : null;
        state.plot = plot ? { left: plot[0], right: plot[1], top: plot[2], bottom: plot[3] } : null;
        state.index = new Map();
        if (state.readout)
            state.readout.columns.forEach((column, c) => { for (const entry of column[2]) if (!state.index.has(`${entry[0]}:${entry[1]}`)) state.index.set(`${entry[0]}:${entry[1]}`, c); });
        const svg = drawing();
        if (svg !== state.svg) {
            if (sized && state.svg) sized.unobserve(state.svg);
            state.svg = svg;
            state.overlay = null;
            if (state.brush?.band) unbrush();
            state.brush = null;
            hide();
            if (sized && svg) sized.observe(svg);
        }
        settle();
        region();
        if (svg && keys && root.classList.contains('lumen-spark')) svg.setAttribute('aria-describedby', keys);
    };

    const bindings = [['click', select], ['keydown', keydown], ['pointerover', over], ['pointermove', moved],
        ['pointerout', out], ['pointerleave', left], ['focusin', focused], ['focusout', blurred],
        ['pointerdown', pressed], ['pointerup', released], ['pointercancel', cancelled]];
    for (const [type, handler] of bindings) root.addEventListener(type, handler);
    handlers.set(root, { bindings, tooltip, state, escaped, sized });
}

/// Tells the script of a chart's new drawing, the shared readout it reads, or null, and the plots a drag zooms across, or null.
export function drawn(root, readout, plot) {
    handlers.get(root)?.state?.drawn(readout, plot);
}

/// Node dragging and selection for a graph. Dragging previews with a transform and commits on release.
export function attachGraph(root, dotnet) {
    let drag = null;
    const locate = event => {
        const matrix = root.querySelector('svg')?.getScreenCTM();
        return matrix ? new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse()) : null;
    };
    const node = target => {
        const found = target instanceof Element ? target.closest('[data-node]') : null;
        return found && root.contains(found) ? found : null;
    };
    const down = event => {
        const element = node(event.target);
        const from = element && !event.button ? locate(event) : null;
        if (!from) return;
        drag = { element, from, origin: element.dataset.position.split(',').map(Number), delta: null };
        element.setPointerCapture?.(event.pointerId);
        event.preventDefault();
    };
    const move = event => {
        if (!drag) return;
        const to = locate(event);
        if (!to) return;
        const delta = { x: to.x - drag.from.x, y: to.y - drag.from.y };
        if (Math.hypot(delta.x, delta.y) > 2) drag.delta = delta;
        drag.element.setAttribute('transform', `translate(${delta.x} ${delta.y})`);
    };
    const up = () => {
        if (!drag) return;
        const { element, origin, delta } = drag;
        drag = null;
        element.removeAttribute('transform');
        if (delta) dotnet.invokeMethodAsync('MoveNode', element.dataset.node, origin[0] + delta.x, origin[1] + delta.y);
        else dotnet.invokeMethodAsync('SelectNode', element.dataset.node);
    };
    const keys = event => {
        const element = node(event.target);
        if (!element) return;
        const step = { ArrowLeft: [-8, 0], ArrowRight: [8, 0], ArrowUp: [0, -8], ArrowDown: [0, 8] }[event.key];
        const [x, y] = element.dataset.position.split(',').map(Number);
        if (step) {
            event.preventDefault();
            dotnet.invokeMethodAsync('MoveNode', element.dataset.node, x + step[0], y + step[1]);
        } else if (['Enter', ' '].includes(event.key)) {
            event.preventDefault();
            dotnet.invokeMethodAsync('SelectNode', element.dataset.node);
        }
    };
    const bindings = [['pointerdown', down], ['pointermove', move], ['pointerup', up], ['pointercancel', up], ['keydown', keys]];
    for (const [type, handler] of bindings) root.addEventListener(type, handler);
    handlers.set(root, { bindings });
}

/// Makes a planner drawn by <LumenPlanner> one tab stop. Its cells (the days of the year or of a month, the weekends of a
/// phone's year, the events of a day) share a roving tab stop: the arrow keys, Home and End move it, Enter or Space opens a
/// cell (a month from the year, a day from a month) or selects an event, and Escape goes back out. A click opens or selects
/// the same way. Events in the year and in a month are not tab stops; a keyboard reaches them by opening their day. The
/// focused cell is ringed in the text colour. Nothing here changes what PlannerSvg.Render drew, only the live page.
export function attachPlanner(root, dotnet) {
    const viewport = root.querySelector(':scope > .lumen-viewport');
    const words = root.querySelector(':scope > .lumen-keys');
    const keys = words ? words.id || (words.id = 'lumen-keys-' + ++described) : null;
    // engaged: the reader is in the planner, so a redraw keeps their focus in the drawing; acted: their own key or click inside
    // the planner asked for the next redraw, so the page may scroll to the focus.
    const state = { lastDate: null, engaged: false, acted: false, ring: null };
    const drawing = () => viewport.querySelector('svg');
    const zoom = () => root.dataset.zoom;
    const narrow = () => root.dataset.layout === 'narrow';
    const selector = () => zoom() === 'day' ? '.lumen-datum[data-event], g.lumen-day[data-day]'
        : zoom() === 'year' && narrow() ? 'g.lumen-week[data-weekend]' : 'g.lumen-day[data-day]';
    // A day's cells are its events; a day with none is its own single cell, so Escape still reaches it.
    const cells = () => {
        if (zoom() !== 'day') return [...viewport.querySelectorAll(selector())];
        const events = [...viewport.querySelectorAll('.lumen-datum[data-event]')];
        return events.length ? events : [...viewport.querySelectorAll('g.lumen-day[data-day]')];
    };
    const cellOf = target => {
        const cell = target && target.closest ? target.closest(selector()) : null;
        return cell && viewport.contains(cell) ? cell : null;
    };
    // A phone's month with nothing scheduled draws no cell, so the drawing itself is the stop and stands for the cell.
    const bare = target => !!target && target === drawing() && cells().length === 0;
    const stopOf = target => cellOf(target) || (bare(target) ? target : null);
    const dateOf = cell => cell?.dataset.day || cell?.dataset.weekend || null;
    const rove = cell => {
        // The cell that holds the tab stop is the one that a screen reader reads the keys with.
        for (const other of cells()) {
            other.setAttribute('tabindex', other === cell ? '0' : '-1');
            if (keys && other === cell) other.setAttribute('aria-describedby', keys); else other.removeAttribute('aria-describedby');
        }
        const date = dateOf(cell);
        if (date) state.lastDate = date;
    };
    const unring = () => { state.ring?.remove(); state.ring = null; };
    const ring = cell => {
        unring();
        const svg = drawing();
        if (!svg || !cell || cell.matches('.lumen-datum')) return;   // an event draws its own ring
        const box = cell.getBBox();
        const rect = document.createElementNS(NS, 'rect');
        const attributes = { class: 'lumen-ring', x: box.x - 2, y: box.y - 2, width: box.width + 4, height: box.height + 4, rx: 3,
            fill: 'none', stroke: 'currentColor', 'stroke-width': 2, 'pointer-events': 'none', 'aria-hidden': 'true' };
        for (const [name, value] of Object.entries(attributes)) rect.setAttribute(name, String(value));
        svg.appendChild(rect);
        state.ring = rect;
    };
    // A weekend across a month's end stands in both months' bars under one key; the second opens the later month. Each call
    // names the zoom it was made in, so one that arrives after the planner has zoomed (a second click, a held key) does nothing.
    const open = cell => {
        if (cell.dataset.weekend) {
            const same = cells().filter(other => other.dataset.weekend === cell.dataset.weekend);
            dotnet.invokeMethodAsync('Open', cell.dataset.weekend, same.indexOf(cell) > 0, zoom());
        } else if (cell.dataset.day) dotnet.invokeMethodAsync('Open', cell.dataset.day, false, zoom());
    };
    const shift = (iso, days) => { const d = new Date(iso + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
    const shiftMonth = (iso, months) => {
        const d = new Date(iso + 'T00:00:00Z');
        const first = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + months, 1));
        const last = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth() + 1, 0)).getUTCDate();
        first.setUTCDate(Math.min(d.getUTCDate(), last));
        return first.toISOString().slice(0, 10);
    };
    const byDate = iso => viewport.querySelector(`g.lumen-day[data-day="${iso}"]`);
    const slot = (cell, axis) => Number(cell.querySelector('rect')?.getAttribute(axis) ?? 0);
    const target = (cell, key) => {
        const all = cells(), i = all.indexOf(cell);
        const back = key === 'ArrowLeft' || key === 'ArrowUp';
        if (zoom() === 'day' || (zoom() === 'month' && narrow()))
            return key === 'Home' ? all[0] : key === 'End' ? all[all.length - 1] : all[i + (back ? -1 : 1)];
        if (zoom() === 'year' && narrow()) {
            if (key === 'ArrowLeft' || key === 'ArrowRight') return all[i + (back ? -1 : 1)];
            const row = all.filter(other => slot(other, 'y') === slot(cell, 'y'));
            if (key === 'Home') return row[0];
            if (key === 'End') return row[row.length - 1];
            const rows = [...new Set(all.map(other => slot(other, 'y')))].sort((a, b) => a - b);
            const next = rows[rows.indexOf(slot(cell, 'y')) + (back ? -1 : 1)];
            if (next === undefined) return null;
            return all.filter(other => slot(other, 'y') === next)
                .reduce((best, other) => !best || Math.abs(slot(other, 'x') - slot(cell, 'x')) < Math.abs(slot(best, 'x') - slot(cell, 'x')) ? other : best, null);
        }
        const day = cell.dataset.day, month = all.filter(other => other.dataset.day.slice(0, 7) === day.slice(0, 7));
        switch (key) {
            case 'ArrowLeft': return byDate(shift(day, -1));
            case 'ArrowRight': return byDate(shift(day, 1));
            case 'ArrowUp': return byDate(zoom() === 'year' ? shiftMonth(day, -1) : shift(day, -7));
            case 'ArrowDown': return byDate(zoom() === 'year' ? shiftMonth(day, 1) : shift(day, 7));
            case 'Home': return month[0];
            case 'End': return month[month.length - 1];
        }
        return null;
    };
    const moves = ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
    // A held key repeats its keydown: Enter, Space and Escape act on the first only, as a double-click's first click does.
    const keydown = event => {
        const enter = event.key === 'Enter' || event.key === ' ';
        // An event is a cell only in a day, but one a click focused in the year or a month answers Enter or Space all the same.
        if (enter && event.target.matches?.('.lumen-datum[data-event]') && viewport.contains(event.target)) {
            event.preventDefault();
            if (event.repeat) return;
            state.engaged = true;
            dotnet.invokeMethodAsync('SelectEvent', event.target.dataset.event);
            return;
        }
        const cell = stopOf(event.target);
        if (!cell || event.target !== cell) return;
        if (enter) {
            event.preventDefault();
            if (event.repeat || zoom() === 'day' || bare(cell)) return;
            state.engaged = state.acted = true;
            open(cell);
        } else if (event.key === 'Escape') {
            if (zoom() === 'year') return;
            event.preventDefault();
            if (event.repeat) return;
            state.engaged = state.acted = true;
            dotnet.invokeMethodAsync('Back', dateOf(cell), zoom());
        } else if (moves.includes(event.key) && !bare(cell)) {
            event.preventDefault();
            const next = target(cell, event.key);
            if (next) { rove(next); next.focus(); }
        }
    };
    // A click on an event selects it; a click anywhere else in a cell opens it, and a click on a mark drawn over a cell (a
    // "+N") opens the cell beneath. A click on any button of the planner (the toolbar, a chip) engages it too, so that a button the
    // redraw disables does not leave the focus on the page. The second click of a double-click is not a click of its own.
    const click = event => {
        if (event.target.closest?.('button') && root.contains(event.target)) state.engaged = state.acted = true;
        if (event.detail > 1 || !viewport.contains(event.target)) return;
        const mark = event.target.closest('.lumen-datum[data-event]');
        if (mark) {
            if (zoom() === 'day') rove(mark);
            dotnet.invokeMethodAsync('SelectEvent', mark.dataset.event);
            return;
        }
        if (zoom() === 'day') return;
        const cell = cellOf(event.target) || document.elementsFromPoint(event.clientX, event.clientY).map(cellOf).find(Boolean);
        if (!cell) return;
        state.engaged = state.acted = true;
        rove(cell);
        open(cell);
    };
    // Focus anywhere in the planner engages it, a reader who only uses the arrow keys included.
    const focusin = event => {
        state.engaged = true;
        const cell = stopOf(event.target);
        if (cell && (bare(cell) || cells().includes(cell))) { rove(cell); ring(cell); }
    };
    const focusout = event => {
        if (!stopOf(event.relatedTarget)) unring();
        if (event.relatedTarget && !root.contains(event.relatedTarget)) state.engaged = state.acted = false;
    };
    // A press anywhere outside the planner ends its engagement too, on words that take no focus as well as on a control.
    const outside = event => { if (!root.contains(event.target)) state.engaged = state.acted = false; };
    document.addEventListener('pointerdown', outside, true);
    // The drawing fits its box, so the viewport is a tab stop and a region only while a box narrower than 320 pixels scrolls it.
    const region = () => {
        if (viewport.scrollWidth > viewport.clientWidth + 1) {
            viewport.setAttribute('tabindex', '0'); viewport.setAttribute('role', 'region'); viewport.setAttribute('aria-label', 'Scrollable planner');
        } else { viewport.removeAttribute('tabindex'); viewport.removeAttribute('role'); viewport.removeAttribute('aria-label'); }
    };
    const sized = new ResizeObserver(region);
    sized.observe(viewport);
    // If the reader is in the planner and the redraw took their focus, it follows to the new stop. The page scrolls to it only
    // when the reader's own key or click in the planner asked for the redraw, not for a new width, theme or spec from the host.
    const hold = stop => {
        const acted = state.acted;
        state.acted = false;
        const active = document.activeElement;
        // A button the redraw has just disabled still holds the focus for a moment before the browser drops it to the page.
        if (state.engaged && (!active || active === document.body || active.disabled)) stop.focus(acted ? undefined : { preventScroll: true });
    };
    // Each new drawing: every event leaves the tab order, the cell for the date asked for (or the last one focused) takes the
    // stop, and if the drawing was replaced under the reader's focus, the focus follows to that cell.
    state.settle = focus => {
        unring();
        const svg = drawing();
        if (!svg) return;
        if (keys) svg.setAttribute('aria-describedby', keys);
        for (const mark of svg.querySelectorAll('.lumen-datum')) mark.setAttribute('tabindex', '-1');
        region();
        const all = cells();
        if (all.length === 0) {
            svg.setAttribute('tabindex', '0');
            hold(svg);
            return;
        }
        const date = focus || state.lastDate;
        let current = all[0];
        if (date && zoom() === 'year' && narrow()) {
            // A weekend across a month's end stands in both months' bars: its first copy is in its key's month, the second in the next.
            const seen = new Set();
            const barOf = cell => {
                const key = cell.dataset.weekend, again = seen.has(key);
                seen.add(key);
                return again ? new Date(Date.UTC(+key.slice(0, 4), +key.slice(5, 7), 1)).toISOString().slice(0, 7) : key.slice(0, 7);
            };
            const bars = all.map(barOf);
            const bar = all.filter((cell, i) => bars[i] === date.slice(0, 7));
            current = bar.length
                ? bar.filter(cell => cell.dataset.weekend <= date).pop() || bar[0]
                : all.filter(cell => cell.dataset.weekend <= date).pop() || all[0];
        } else if (date && zoom() !== 'day')
            current = all.find(cell => cell.dataset.day >= date) || all[all.length - 1];
        rove(current);
        hold(current);
    };
    const bindings = [['keydown', keydown], ['click', click], ['focusin', focusin], ['focusout', focusout]];
    for (const [type, handler] of bindings) root.addEventListener(type, handler);
    handlers.set(root, { bindings, sized, planner: state, outside });
    state.settle(null);
}

/// Tells a planner's script that its drawing changed, and which date (yyyy-MM-dd), if any, should hold its tab stop.
export function plannerDrawn(root, focus) {
    handlers.get(root)?.planner?.settle(focus);
}

/// Reports the width of a chart's or a graph's box in whole pixels to the component's Fit method, at once and again whenever
/// the box settles at a new width, so that it can be drawn at the width it is shown and its text keeps its own size. A hidden
/// box measures nothing.
export function fit(root, dotnet) {
    unfit(root);
    let timer = 0, last = 0;
    const measure = () => {
        const box = root.querySelector(':scope > .lumen-viewport') || root;
        const width = box.clientWidth;
        if (!width || width === last) return;
        last = width;
        dotnet.invokeMethodAsync('Fit', width);
    };
    const observer = new ResizeObserver(() => { clearTimeout(timer); timer = setTimeout(measure, 150); });
    observer.observe(root);
    fits.set(root, { observer, stop: () => clearTimeout(timer) });
    measure();
}

export function unfit(root) {
    const state = fits.get(root);
    if (!state) return;
    state.observer.disconnect();
    state.stop();
    fits.delete(root);
}

export function detach(root) {
    unfit(root);
    const state = handlers.get(root);
    if (!state) return;
    for (const [type, handler] of state.bindings) root.removeEventListener(type, handler);
    state.tooltip?.remove();
    clearTimeout(state.state?.timer);
    state.state?.overlay?.remove();
    state.state?.brush?.band?.remove();
    state.sized?.disconnect();
    if (state.escaped) document.removeEventListener('keydown', state.escaped, true);
    if (state.outside) document.removeEventListener('pointerdown', state.outside, true);
    handlers.delete(root);
}

/// Reads the host's custom properties as they apply at `root` and normalizes each to #RRGGBB.
/// Accepts anything the browser accepts as a colour, plus Bootstrap-style "13, 110, 253" triples.
/// Transparency is discarded: a chart colour is drawn opaque.
export function resolveBrand(root, request) {
    const probe = document.createElement('span');
    probe.style.display = 'none';
    root.appendChild(probe);
    const host = getComputedStyle(root);
    const colour = name => {
        const raw = name ? host.getPropertyValue(name).trim() : '';
        if (!raw) return null;
        probe.style.color = '';
        probe.style.color = raw;
        if (!probe.style.color) probe.style.color = `rgb(${raw})`;
        if (!probe.style.color) return null;
        const channels = getComputedStyle(probe).color.match(/[\d.]+/g);
        return channels ? '#' + channels.slice(0, 3).map(v => Math.round(+v).toString(16).padStart(2, '0')).join('').toUpperCase() : null;
    };
    const result = {
        series: (request.series || []).map(colour),
        background: colour(request.background), text: colour(request.text), muted: colour(request.muted),
        grid: colour(request.grid), rising: colour(request.rising), falling: colour(request.falling),
        font: request.font ? host.fontFamily : null
    };
    probe.remove();
    return result;
}

export function download(content, filename, type) {
    save(new Blob([content], { type }), filename);
}

/// Rasterizes a self-contained SVG document through a canvas. Text uses fonts available to the browser.
export function png(svg, filename, scale) {
    return new Promise((resolve, reject) => {
        const document_ = new DOMParser().parseFromString(svg, 'image/svg+xml');
        const root = document_.documentElement;
        if (root.getElementsByTagName('parsererror').length) { reject(new Error('The chart SVG could not be parsed.')); return; }
        const box = (root.getAttribute('viewBox') || '').split(/[\s,]+/).map(Number);
        const width = box[2] > 0 ? box[2] : 900, height = box[3] > 0 ? box[3] : 420;
        // Firefox rasterizes only when the document carries intrinsic dimensions.
        root.setAttribute('width', width);
        root.setAttribute('height', height);
        const factor = Math.max(1, Math.min(scale || 2, 8192 / Math.max(width, height)));
        const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(root)], { type: 'image/svg+xml;charset=utf-8' }));
        const image = new Image();
        image.onload = () => {
            try {
                const canvas = document.createElement('canvas');
                canvas.width = Math.round(width * factor);
                canvas.height = Math.round(height * factor);
                const context = canvas.getContext('2d');
                // A drawing left unpainted, which carries its colour as --lumen-ground instead, stays transparent.
                const ground = root.style.background || (root.style.getPropertyValue('--lumen-ground') ? '' : '#FFFFFF');
                if (ground) {
                    context.fillStyle = ground;
                    context.fillRect(0, 0, canvas.width, canvas.height);
                }
                context.drawImage(image, 0, 0, canvas.width, canvas.height);
                canvas.toBlob(blob => {
                    if (!blob) { reject(new Error('The browser produced no image data.')); return; }
                    save(blob, filename);
                    resolve();
                }, 'image/png');
            } catch (error) { reject(error); }
            finally { URL.revokeObjectURL(url); }
        };
        image.onerror = () => { URL.revokeObjectURL(url); reject(new Error('The browser could not rasterize the chart.')); };
        image.src = url;
    });
}

function save(blob, filename) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a'); link.href = url; link.download = filename;
    document.body.appendChild(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
