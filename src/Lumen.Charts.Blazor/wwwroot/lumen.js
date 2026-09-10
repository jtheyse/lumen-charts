const handlers = new WeakMap();

export function attach(root, dotnet) {
    const tooltip = document.createElement('div');
    tooltip.className = 'lumen-tooltip';
    tooltip.setAttribute('aria-hidden', 'true');
    tooltip.hidden = true;
    root.appendChild(tooltip);

    const mark = target => {
        const found = target instanceof Element ? target.closest('.lumen-datum') : null;
        return found && root.contains(found) ? found : null;
    };
    const hide = () => { tooltip.hidden = true; };
    const place = (element, event) => {
        const bounds = root.getBoundingClientRect();
        const box = element.getBoundingClientRect();
        const x = event && event.clientX ? event.clientX : box.left + box.width / 2;
        const y = event && event.clientY ? Math.min(event.clientY, box.top + box.height / 2) : box.top;
        tooltip.style.left = Math.min(Math.max(x - bounds.left, 60), bounds.width - 60) + 'px';
        tooltip.style.top = Math.max(y - bounds.top - 12, 26) + 'px';
    };
    const show = event => {
        const element = mark(event.target);
        if (!element) { hide(); return; }
        tooltip.textContent = element.getAttribute('aria-label') || '';
        tooltip.hidden = false;
        place(element, event.clientX ? event : null);
    };
    const move = event => { if (!tooltip.hidden) show(event); };
    const leave = event => { if (!mark(event.relatedTarget)) hide(); };
    const select = event => {
        if (event.type === 'keydown' && !['Enter', ' '].includes(event.key)) {
            if (event.key === 'Escape') hide();
            return;
        }
        const point = event.target.closest('[data-point]');
        if (!point || !root.contains(point)) return;
        if (event.type === 'keydown') event.preventDefault();
        dotnet.invokeMethodAsync('SelectPoint', Number(point.dataset.series), Number(point.dataset.point));
    };

    const bindings = [['click', select], ['keydown', select], ['pointerover', show], ['pointermove', move],
        ['pointerout', leave], ['focusin', show], ['focusout', leave]];
    for (const [type, handler] of bindings) root.addEventListener(type, handler);
    handlers.set(root, { bindings, tooltip });
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

export function detach(root) {
    const state = handlers.get(root);
    if (!state) return;
    for (const [type, handler] of state.bindings) root.removeEventListener(type, handler);
    state.tooltip?.remove();
    handlers.delete(root);
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
                context.fillStyle = root.style.background || '#FFFFFF';
                context.fillRect(0, 0, canvas.width, canvas.height);
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
