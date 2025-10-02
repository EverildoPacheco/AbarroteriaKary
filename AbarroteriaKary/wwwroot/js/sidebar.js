

// =====================================
// sidebar.js  (Abarrotería Kary)
// - Móvil: abre/cierra como offcanvas (clase --sidebar-open en <body>)
// - Escritorio: colapsa/expande ancho (clase --sidebar-collapsed en <body>)
// - Persiste estado en localStorage
// - Cambia icono de la hamburguesa según estado
// - Submenús: acordeón + auto-abrir el grupo del item activo
// =====================================
(function () {
    'use strict';

    const BTN = document.getElementById('btnSidebarToggle'); // botón hamburguesa del header
    const SIDEBAR = document.getElementById('karySidebar');  // aside del menú (en _Sidebar)
    const OVERLAY = document.getElementById('karyOverlay');  // capa oscura (móvil)

    const CLS_OPEN = '--sidebar-open';       // móvil visible
    const CLS_COL = '--sidebar-collapsed';  // escritorio colapsado
    const STORE = 'kary.sidebar.state';   // 'expanded' | 'collapsed'

    if (!SIDEBAR) return;

    // Debe coincidir con el @media del CSS
    const isMobile = () => window.matchMedia('(max-width: 992px)').matches;

    // ======== Icono dinámico del botón (Font Awesome) ========
    // Espera un <i> dentro del botón. Si no existe, no hace nada.
    const ICON = BTN ? BTN.querySelector('i') : null;

    function setIcon(name) {
        if (!ICON) return;
        ICON.classList.remove('fa-bars', 'fa-xmark', 'fa-angles-right');
        ICON.classList.add('fa-' + name);
    }

    function updateHamburgerIcon() {
        if (!ICON) return;
        if (isMobile()) {
            // En móvil: X cuando el offcanvas está abierto; barras cuando está cerrado
            document.body.classList.contains(CLS_OPEN) ? setIcon('xmark') : setIcon('bars');
        } else {
            // En escritorio: » cuando está colapsado; barras cuando está expandido
            document.body.classList.contains(CLS_COL) ? setIcon('angles-right') : setIcon('bars');
        }
    }

    // --- Acciones (móvil) ---
    function openMobile() {
        document.body.classList.add(CLS_OPEN);
        setAriaExpanded(true);
        showOverlay(true);
        bindDismiss(true);
        updateHamburgerIcon();
    }
    function closeMobile() {
        document.body.classList.remove(CLS_OPEN);
        setAriaExpanded(false);
        showOverlay(false);
        bindDismiss(false);
        updateHamburgerIcon();
    }
    function toggleMobile() {
        document.body.classList.contains(CLS_OPEN) ? closeMobile() : openMobile();
    }

    // --- Acciones (escritorio) ---
    function toggleDesktopCollapsed() {
        const collapsed = document.body.classList.toggle(CLS_COL);
        localStorage.setItem(STORE, collapsed ? 'collapsed' : 'expanded');
        setAriaExpanded(!collapsed);
        updateHamburgerIcon();
    }

    // --- Utilidades ---
    function setAriaExpanded(expanded) {
        if (BTN) BTN.setAttribute('aria-expanded', String(!!expanded));
    }
    function showOverlay(show) {
        if (!OVERLAY) return;
        OVERLAY.hidden = !show;
    }

    // Cerrar con ESC o clic fuera (sólo móvil)
    function onKeydown(e) { if (e.key === 'Escape' && isMobile()) closeMobile(); }
    function onOverlayClick() { closeMobile(); }
    function bindDismiss(bind) {
        if (!OVERLAY) return;
        if (bind) {
            document.addEventListener('keydown', onKeydown);
            OVERLAY.addEventListener('click', onOverlayClick, { once: true });
        } else {
            document.removeEventListener('keydown', onKeydown);
        }
    }

    // Restaurar estado en escritorio
    function restoreDesktopState() {
        if (isMobile()) {
            document.body.classList.remove(CLS_COL); // en móvil no hay colapso
            setAriaExpanded(false);
            return;
        }
        const saved = localStorage.getItem(STORE);
        const collapsed = (saved === 'collapsed');
        document.body.classList.toggle(CLS_COL, collapsed);
        setAriaExpanded(!collapsed);
    }

    // Eventos
    if (BTN) {
        BTN.addEventListener('click', function () {
            isMobile() ? toggleMobile() : toggleDesktopCollapsed();
        });
    }
    window.addEventListener('resize', function () {
        // Si cambia a escritorio, aseguremos que el overlay esté oculto
        if (!isMobile()) {
            showOverlay(false);
            document.body.classList.remove(CLS_OPEN);
        }
        restoreDesktopState();
        updateHamburgerIcon();
    });

    // Init
    restoreDesktopState();
    updateHamburgerIcon();

})();


// =====================================
// Submenús del sidebar (colapsables)
// - Acordeón: solo un grupo abierto a la vez
// - Autoabrir el grupo que contiene el link .is-active
// - Persistencia por grupo (localStorage)
// =====================================
(function () {
    'use strict';

    const groups = document.querySelectorAll('.kary-menu__group');
    if (!groups.length) return;

    const SIDEBAR_COLLAPSED_CLASS = '--sidebar-collapsed';
    const ACCORDION = true; // ← si desea desactivar acordeón, ponga false

    // 1) Marcar el grupo que contiene el link activo y abrirlo
    markActiveAndEnsureOpen();

    // 2) Inicializar listeners y restaurar estados
    groups.forEach(group => {
        const btn = group.querySelector('.js-submenu-toggle');
        const panel = group.querySelector('.kary-submenu');
        if (!btn || !panel) return;

        const key = storageKey(group, panel);

        // Restaurar estado guardado si el sidebar NO está colapsado
        const saved = localStorage.getItem(key);
        if (saved === 'open' && !document.body.classList.contains(SIDEBAR_COLLAPSED_CLASS)) {
            openGroup(group, btn, panel, false);
        }

        btn.addEventListener('click', () => {
            // Si el sidebar está colapsado en escritorio, no desplegar submenús
            if (document.body.classList.contains(SIDEBAR_COLLAPSED_CLASS)) return;

            const isOpen = group.classList.contains('is-open');
            if (isOpen) {
                closeGroup(group, btn, panel, true);
            } else {
                if (ACCORDION) closeSiblings(group);
                openGroup(group, btn, panel, true);
            }
        });
    });

    // ---- helpers ----
    function storageKey(group, panel) {
        return 'kary.submenu.' + (group.dataset.group || panel.id || '');
    }

    function setMaxHeight(panel, h) {
        panel.style.maxHeight = (h == null ? '' : h + 'px');
    }

    function openGroup(group, btn, panel, persist) {
        group.classList.add('is-open');
        btn.setAttribute('aria-expanded', 'true');
        panel.hidden = false;                          // visible para medir scrollHeight
        setMaxHeight(panel, panel.scrollHeight);       // anima hasta contenido
        if (persist) save(group, 'open');
    }

    function closeGroup(group, btn, panel, persist) {
        group.classList.remove('is-open');
        btn.setAttribute('aria-expanded', 'false');

        setMaxHeight(panel, panel.scrollHeight);
        requestAnimationFrame(() => setMaxHeight(panel, 0));

        const onEnd = (e) => {
            if (e.propertyName !== 'max-height') return;
            panel.hidden = true;
            panel.removeEventListener('transitionend', onEnd);
        };
        panel.addEventListener('transitionend', onEnd);

        if (persist) save(group, 'closed');
    }

    function save(group, state) {
        try {
            const panel = group.querySelector('.kary-submenu');
            localStorage.setItem(storageKey(group, panel), state);
        } catch { }
    }

    // Cierra todos los hermanos del grupo actual (acordeón)
    function closeSiblings(currentGroup) {
        document.querySelectorAll('.kary-menu__group.is-open').forEach(g => {
            if (g === currentGroup) return;
            const btn = g.querySelector('.js-submenu-toggle');
            const panel = g.querySelector('.kary-submenu');
            if (btn && panel) closeGroup(g, btn, panel, true);
        });
    }

    // Marca el padre del link activo y lo abre (si no está colapsado)
    function markActiveAndEnsureOpen() {
        const actives = document.querySelectorAll('.kary-submenu__item.is-active');
        if (!actives.length) return;

        actives.forEach(a => {
            const group = a.closest('.kary-menu__group');
            if (!group) return;

            // Marca para estilos (resalte del botón padre)
            group.classList.add('has-active');

            // Si el sidebar no está colapsado, abrir para que se vea el activo
            if (!document.body.classList.contains(SIDEBAR_COLLAPSED_CLASS)) {
                const btn = group.querySelector('.js-submenu-toggle');
                const panel = group.querySelector('.kary-submenu');
                if (btn && panel && !group.classList.contains('is-open')) {
                    if (ACCORDION) closeSiblings(group);
                    openGroup(group, btn, panel, true);
                }
            }
        });
    }

    // Si cambian a modo colapsado (o vuelven), cerrar submenús abiertos
    window.addEventListener('resize', collapseGuard);
    document.addEventListener('DOMContentLoaded', collapseGuard);

    function collapseGuard() {
        const collapsed = document.body.classList.contains(SIDEBAR_COLLAPSED_CLASS);
        if (!collapsed) return;
        document.querySelectorAll('.kary-menu__group.is-open .kary-submenu').forEach(panel => {
            panel.hidden = true;
            panel.style.maxHeight = '0px';
            const group = panel.parentElement;
            group?.classList.remove('is-open');
            const btn = group?.querySelector('.js-submenu-toggle');
            if (btn) btn.setAttribute('aria-expanded', 'false');
        });
    }
})();













//// =====================================
//// sidebar.js  (Abarrotería Kary)
//// - Móvil: abre/cierra como offcanvas (clase --sidebar-open en <body>)
//// - Escritorio: colapsa/expande ancho (clase --sidebar-collapsed en <body>)
//// - Persiste estado en localStorage
//// - Cambia icono de la hamburguesa según estado
//// - Maneja submenús colapsables (segunda IIFE)
//// =====================================
//(function () {
//    'use strict';

//    const BTN = document.getElementById('btnSidebarToggle'); // botón hamburguesa del header
//    const SIDEBAR = document.getElementById('karySidebar');      // aside del menú (en _Sidebar)
//    const OVERLAY = document.getElementById('karyOverlay');      // capa oscura (móvil)

//    const CLS_OPEN = '--sidebar-open';            // móvil visible
//    const CLS_COL = '--sidebar-collapsed';       // escritorio colapsado
//    const STORE = 'kary.sidebar.state';        // 'expanded' | 'collapsed'

//    if (!SIDEBAR) return;

//    // Debe coincidir con el @media del CSS
//    const isMobile = () => window.matchMedia('(max-width: 992px)').matches;

//    // ======== Icono dinámico del botón (Font Awesome) ========
//    // Espera un <i> dentro del botón. Si no existe, no hace nada.
//    const ICON = BTN ? BTN.querySelector('i') : null;

//    function setIcon(name) {
//        if (!ICON) return;
//        ICON.classList.remove('fa-bars', 'fa-xmark', 'fa-angles-right');
//        ICON.classList.add('fa-' + name);
//    }

//    function updateHamburgerIcon() {
//        if (!ICON) return;
//        if (isMobile()) {
//            // En móvil: X cuando el offcanvas está abierto; barras cuando está cerrado
//            document.body.classList.contains(CLS_OPEN) ? setIcon('xmark') : setIcon('bars');
//        } else {
//            // En escritorio: » cuando está colapsado; barras cuando está expandido
//            document.body.classList.contains(CLS_COL) ? setIcon('angles-right') : setIcon('bars');
//        }
//    }

//    // --- Acciones (móvil) ---
//    function openMobile() {
//        document.body.classList.add(CLS_OPEN);
//        setAriaExpanded(true);
//        showOverlay(true);
//        bindDismiss(true);
//        updateHamburgerIcon();
//    }
//    function closeMobile() {
//        document.body.classList.remove(CLS_OPEN);
//        setAriaExpanded(false);
//        showOverlay(false);
//        bindDismiss(false);
//        updateHamburgerIcon();
//    }
//    function toggleMobile() {
//        document.body.classList.contains(CLS_OPEN) ? closeMobile() : openMobile();
//    }

//    // --- Acciones (escritorio) ---
//    function toggleDesktopCollapsed() {
//        const collapsed = document.body.classList.toggle(CLS_COL);
//        localStorage.setItem(STORE, collapsed ? 'collapsed' : 'expanded');
//        setAriaExpanded(!collapsed);
//        updateHamburgerIcon();
//    }

//    // --- Utilidades ---
//    function setAriaExpanded(expanded) {
//        if (BTN) BTN.setAttribute('aria-expanded', String(!!expanded));
//    }
//    function showOverlay(show) {
//        if (!OVERLAY) return;
//        OVERLAY.hidden = !show;
//    }

//    // Cerrar con ESC o clic fuera (sólo móvil)
//    function onKeydown(e) { if (e.key === 'Escape' && isMobile()) closeMobile(); }
//    function onOverlayClick() { closeMobile(); }
//    function bindDismiss(bind) {
//        if (!OVERLAY) return;
//        if (bind) {
//            document.addEventListener('keydown', onKeydown);
//            OVERLAY.addEventListener('click', onOverlayClick, { once: true });
//        } else {
//            document.removeEventListener('keydown', onKeydown);
//        }
//    }

//    // Restaurar estado en escritorio
//    function restoreDesktopState() {
//        if (isMobile()) {
//            document.body.classList.remove(CLS_COL); // en móvil no hay colapso
//            setAriaExpanded(false);
//            return;
//        }
//        const saved = localStorage.getItem(STORE);
//        const collapsed = (saved === 'collapsed');
//        document.body.classList.toggle(CLS_COL, collapsed);
//        setAriaExpanded(!collapsed);
//    }

//    // Eventos
//    if (BTN) {
//        BTN.addEventListener('click', function () {
//            isMobile() ? toggleMobile() : toggleDesktopCollapsed();
//        });
//    }
//    window.addEventListener('resize', function () {
//        // Si cambia a escritorio, aseguremos que el overlay esté oculto
//        if (!isMobile()) {
//            showOverlay(false);
//            document.body.classList.remove(CLS_OPEN);
//        }
//        restoreDesktopState();
//        updateHamburgerIcon();
//    });

//    // Init
//    restoreDesktopState();
//    updateHamburgerIcon();

//})();


//// =====================================
//// Submenús del sidebar (colapsables)
//// =====================================
//(function () {
//    'use strict';

//    // Todos los grupos con submenú
//    const groups = document.querySelectorAll('.kary-menu__group');
//    if (!groups.length) return;

//    groups.forEach(group => {
//        const btn = group.querySelector('.js-submenu-toggle');
//        const panel = group.querySelector('.kary-submenu');
//        if (!btn || !panel) return;

//        // Clave de persistencia por grupo (opcional)
//        const key = 'kary.submenu.' + (group.dataset.group || panel.id);

//        // Restaurar estado guardado si no está colapsado el sidebar
//        const saved = localStorage.getItem(key);
//        if (saved === 'open' && !document.body.classList.contains('--sidebar-collapsed')) {
//            openGroup(group, btn, panel, false);
//        }

//        // Toggle al hacer click
//        btn.addEventListener('click', () => {
//            // Si el sidebar está colapsado en escritorio, ignoramos la apertura
//            if (document.body.classList.contains('--sidebar-collapsed')) return;

//            const isOpen = group.classList.contains('is-open');
//            isOpen ? closeGroup(group, btn, panel, true)
//                : openGroup(group, btn, panel, true);
//        });
//    });

//    // --- helpers ---
//    function setMaxHeight(panel, h) {
//        panel.style.maxHeight = (h == null ? '' : h + 'px');
//    }

//    function openGroup(group, btn, panel, persist) {
//        group.classList.add('is-open');
//        btn.setAttribute('aria-expanded', 'true');
//        panel.hidden = false;                          // visible para medir scrollHeight
//        setMaxHeight(panel, panel.scrollHeight);       // anima hasta contenido
//        if (persist) save(group, 'open');
//    }

//    function closeGroup(group, btn, panel, persist) {
//        group.classList.remove('is-open');
//        btn.setAttribute('aria-expanded', 'false');
//        // animar cierre desde la altura actual a 0
//        setMaxHeight(panel, panel.scrollHeight);
//        requestAnimationFrame(() => setMaxHeight(panel, 0));
//        // al terminar la transición, lo marcamos hidden para accesibilidad
//        const onEnd = (e) => {
//            if (e.propertyName !== 'max-height') return;
//            panel.hidden = true;
//            panel.removeEventListener('transitionend', onEnd);
//        };
//        panel.addEventListener('transitionend', onEnd);
//        if (persist) save(group, 'closed');
//    }

//    function save(group, state) {
//        const key = 'kary.submenu.' + (group.dataset.group || group.querySelector('.kary-submenu')?.id || '');
//        try { localStorage.setItem(key, state); } catch { }
//    }

//    // Si cambian a modo colapsado (o vuelven), cerrar submenús
//    window.addEventListener('resize', collapseGuard);
//    document.addEventListener('DOMContentLoaded', collapseGuard);

//    function collapseGuard() {
//        const collapsed = document.body.classList.contains('--sidebar-collapsed');
//        if (!collapsed) return;
//        document.querySelectorAll('.kary-menu__group.is-open .kary-submenu').forEach(panel => {
//            panel.hidden = true;
//            panel.style.maxHeight = '0px';
//            panel.parentElement.classList.remove('is-open');
//            const btn = panel.parentElement.querySelector('.js-submenu-toggle');
//            if (btn) btn.setAttribute('aria-expanded', 'false');
//        });
//    }

//})();
