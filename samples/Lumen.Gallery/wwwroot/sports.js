// Reports how wide a full-width card and a half-width card are on the Sports & performance page, so the page can draw each
// chart at the width it is shown and its text stays at its own size, on a phone as on a desktop. It reports again whenever
// the layout settles at new widths.
const observers = new WeakMap();

export function watch(page, dotnet) {
    let timer = 0, last = '';
    const measure = () => {
        const wide = page.querySelector('.sports-card.wide')?.clientWidth ?? 0;
        const half = page.querySelector('.sports-card.half')?.clientWidth ?? 0;
        const key = `${wide},${half}`;
        if (key === last || !wide || !half) return;
        last = key;
        dotnet.invokeMethodAsync('Measured', wide, half);
    };
    const observer = new ResizeObserver(() => { clearTimeout(timer); timer = setTimeout(measure, 150); });
    observer.observe(page);
    observers.set(page, observer);
    measure();
}

export function unwatch(page) {
    observers.get(page)?.disconnect();
    observers.delete(page);
}
