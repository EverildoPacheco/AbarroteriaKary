// KaryLoading: overlay de carga global (automático)
// Requiere el partial _LoadingOverlay en el _Layout.
window.KaryLoading = (function () {
    const getRoot = () => document.getElementById('kary-loading');
    const getTextEl = () => getRoot()?.querySelector('.kary-loading__text');

    // Evita parpadeos: visibilidad mínima
    let visibleSince = 0;
    const MIN_MS = 100;

    function show(msg) {
        const root = getRoot();
        if (!root) return;

        if (msg && getTextEl()) getTextEl().textContent = msg;

        visibleSince = Date.now();
        root.classList.remove('d-none');
        root.classList.add('is-visible');   // <- si tu CSS usa .is-open, cambia aquí
        root.setAttribute('aria-hidden', 'false');
    }

    function hide() {
        const root = getRoot();
        if (!root) return;

        const elapsed = Date.now() - visibleSince;
        const wait = Math.max(0, MIN_MS - elapsed);
        setTimeout(() => {
            root.classList.remove('is-visible'); // <- o .is-open
            root.classList.add('d-none');
            root.setAttribute('aria-hidden', 'true');
        }, wait);
    }

    // Utilidades
    const hasNoLoading = (el) => !!el?.closest?.('[data-no-loading]');
    const getMsgFrom = (el) => (el?.closest?.('[data-loading-text]') || el)?.getAttribute?.('data-loading-text');

    // ---------- Formularios (solo si son válidos) ----------
    function bindFormsDelegated() {
        document.addEventListener('submit', (ev) => {
            const form = ev.target;
            if (!(form instanceof HTMLFormElement)) return;

            if (hasNoLoading(form)) return;

            // 1) Validación con jQuery Unobtrusive si existe
            let isValid;
            if (window.jQuery && $.validator && typeof $(form).valid === 'function') {
                isValid = $(form).valid();     // NO muestra loading si es false
            } else {
                // 2) Validación HTML5 nativa
                isValid = form.checkValidity();
                if (!isValid) {
                    // Deja que el navegador muestre los errores y NO muestres loading
                    // (si usas Bootstrap, 'was-validated' ayuda a ver feedback)
                    form.classList.add('was-validated');
                }
            }

            if (!isValid) { hide(); return; }  // <-- clave: oculta si hay errores
            show(getMsgFrom(form) || 'Procesando…');
        }, true);

        // Cuando algún control resulta inválido (HTML5), oculta el overlay
        document.addEventListener('invalid', () => hide(), true);
    }

    // ---------- Enlaces ----------
    function bindLinksDelegated() {
        document.addEventListener('click', (ev) => {
            const a = ev.target.closest?.('a[href]');
            if (!a) return;

            // Respeta opt-out y enlaces usados por “cambios sin guardar”
            if (hasNoLoading(a)) return;
            if (a.classList.contains('js-leave')) return;

            const href = a.getAttribute('href') || '';
            if (a.getAttribute('target') === '_blank') return;
            if (a.hasAttribute('download')) return;
            if (!href || href.startsWith('#')) return;

            show(getMsgFrom(a) || 'Cargando…');
        }, true);
    }

    // ---------- jQuery AJAX (si existe) ----------
    function bindJqueryAjax() {
        if (!window.jQuery) return;
        $(document).ajaxStart(() => show('Procesando…'));
        $(document).ajaxStop(() => hide());
        $(document).ajaxError(() => hide());
    }

    // Ignora overlay en AJAX jQuery de notificaciones
    if (window.jQuery) {
        $.ajaxPrefilter(function (options, originalOptions, jqXHR) {
            const url = options.url || '';
            if (url.includes('/Notificaciones/')) {
                jqXHR.setRequestHeader('X-No-Loading', '1');
            }
        });
    }



    // ---------- fetch() global ----------
    //function patchFetch() {
    //    if (!window.fetch || window.fetch.__karyPatched) return;
    //    const _fetch = window.fetch.bind(window);

    //    window.fetch = function (input, init) {
    //        const noLoading =
    //            (init && (init.headers?.['X-No-Loading'] || init['X-No-Loading'])) || false;

    //        if (!noLoading) show('Procesando…');

    //        const p = _fetch(input, init);
    //        p.finally?.(() => hide()); // éxito o error
    //        return p;
    //    };

    //    window.fetch.__karyPatched = true;
    //}
    function patchFetch() {
        if (!window.fetch || window.fetch.__karyPatched) return;

        const _fetch = window.fetch.bind(window);

        // Endpoints que NO deben mostrar overlay
        const IGNORE = [
            '/Notificaciones/Count',
            '/Notificaciones/Dropdown',
            '/Notificaciones/Poll',
            '/Notificaciones/Read',
            '/Notificaciones/ReadAll',
            '/Notificaciones/Snooze',
            '/Notificaciones/Archive'
        ];

        window.fetch = function (input, init = {}) {
            const url = typeof input === 'string' ? input : input.url || '';
            const noLoadingHeader = (init.headers && (init.headers['X-No-Loading'] || init.headers['x-no-loading'])) ? true : false;

            const isNotif = IGNORE.some(x => url.includes(x));
            const noLoading = noLoadingHeader || isNotif;

            if (!noLoading) show('Procesando…');

            const p = _fetch(input, init);
            p.finally?.(() => hide());
            return p;
        };

        window.fetch.__karyPatched = true;
    }

    // ---------- Arranque ----------
    document.addEventListener('DOMContentLoaded', () => {
        bindFormsDelegated();
        bindLinksDelegated();
        bindJqueryAjax();
        patchFetch();

        // Si vuelve desde historial (back/forward), asegúrate de ocultar
        window.addEventListener('pageshow', () => hide());
    });

    return { show, hide };
})();
