// Every chart on the Sports & performance page sets FitWidth, which draws it at the width its card gives it. One choice is
// left to the page: the activity stream marks where the progression steps up only while its card is wide enough for the
// markers' labels to stand clear of the zones', so this reports whether that card is narrower than 600 pixels, and again
// whenever that changes.
const observers = new WeakMap();

export function watch(page, dotnet) {
    const card = page.querySelector('#stream');
    if (!card) return;
    let timer = 0, last = null;
    const measure = () => {
        if (!card.clientWidth) return;
        const narrow = card.clientWidth < 600;
        if (narrow === last) return;
        last = narrow;
        dotnet.invokeMethodAsync('Narrow', narrow);
    };
    const observer = new ResizeObserver(() => { clearTimeout(timer); timer = setTimeout(measure, 150); });
    observer.observe(card);
    observers.set(page, { observer, stop: () => clearTimeout(timer) });
    measure();
}

export function unwatch(page) {
    const state = observers.get(page);
    state?.observer.disconnect();
    state?.stop();
    observers.delete(page);
}
