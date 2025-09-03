// KaryLoading: overlay de carga global (automático)
// Requiere el partial _LoadingOverlay en el _Layout.
window.KaryLoading = (function () {
    const getRoot = () => document.getElementById('kary-loading');
    const getTextEl = () => getRoot()?.querySelector('.kary-loading__text');

    // Para evitar "parpadeos": garantizamos visibilidad mínima
    let visibleSince = 0;
    const MIN_MS = 100; // mínimo 300ms visible

    function show(msg) {
        const root = getRoot();
        if (!root) return;

        if (msg && getTextEl()) getTextEl().textContent = msg;

        visibleSince = Date.now();
        root.classList.remove('d-none');
        root.classList.add('is-visible');
        root.setAttribute('aria-hidden', 'false');
    }

    function hide() {
        const root = getRoot();
        if (!root) return;

        const elapsed = Date.now() - visibleSince;
        const wait = Math.max(0, MIN_MS - elapsed);
        setTimeout(() => {
            root.classList.remove('is-visible');
            root.classList.add('d-none');
            root.setAttribute('aria-hidden', 'true');
        }, wait);
    }

    // Utilidades
    const hasNoLoading = (el) =>
        !!el?.closest?.('[data-no-loading]');

    const getMsgFrom = (el) => {
        const host = el?.closest?.('[data-loading-text]') || el;
        return host?.getAttribute?.('data-loading-text');
    };

    // --------- Auto: formularios (cualquier submit válido) ----------
    function bindFormsDelegated() {
        document.addEventListener('submit', (ev) => {
            const form = ev.target;
            if (!(form instanceof HTMLFormElement)) return;

            // Opt-out por atributo
            if (hasNoLoading(form)) return;

            // Respeta validaciones HTML5/Unobtrusive
            if (!form.checkValidity()) return;

            show(getMsgFrom(form) || 'Procesando…');
        }, true); // capture: true, asegura ejecutarse antes de la navegación
    }

    // --------- Auto: enlaces que navegan ----------
    function bindLinksDelegated() {
        document.addEventListener('click', (ev) => {
            const a = ev.target.closest?.('a[href]');
            if (!a) return;

            // No para _blank, descargas, anclas o elementos con opt-out
            const href = a.getAttribute('href') || '';
            if (hasNoLoading(a)) return;
            if (a.getAttribute('target') === '_blank') return;
            if (a.hasAttribute('download')) return;
            if (!href || href.startsWith('#')) return;

            show(getMsgFrom(a) || 'Cargando…');
            // No prevenimos la navegación; sólo mostramos el overlay.
        }, true);
    }

    // --------- Auto: jQuery AJAX (si existe) ----------
    function bindJqueryAjax() {
        if (!window.jQuery) return;
        $(document).ajaxStart(() => show('Procesando…'));
        $(document).ajaxStop(() => hide());
        $(document).ajaxError(() => hide());
    }

    // --------- Auto: fetch() global ----------
    function patchFetch() {
        if (!window.fetch || window.fetch.__karyPatched) return;

        const _fetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            // Opt-out con cabecera/flag
            const noLoading =
                (init && (init.headers?.['X-No-Loading'] || init['X-No-Loading'])) ||
                false;

            if (!noLoading) show('Procesando…');

            const p = _fetch(input, init);
            // Oculta al terminar (éxito o error)
            p.finally?.(() => hide());
            return p;
        };
        window.fetch.__karyPatched = true;
    }

    // Arranque automático
    document.addEventListener('DOMContentLoaded', () => {
        bindFormsDelegated();
        bindLinksDelegated();
        bindJqueryAjax();
        patchFetch();
    });

    // API pública por si quieres usarlo manualmente en algún caso
    return { show, hide };
})();
