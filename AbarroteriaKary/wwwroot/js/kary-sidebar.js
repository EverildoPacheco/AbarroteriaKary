(function () {
    'use strict';

    // Clases/tokens a reutilizar
    const LAYOUT = document.getElementById('karyLayout');
    const SIDEBAR = document.getElementById('karySidebar');
    const OVERLAY = document.getElementById('karyOverlay');
    const BTN = document.getElementById('btnSidebarToggle');

    const KEY = 'kary.sidebar.state'; // guarda estado escritorio: "expanded" | "collapsed"

    // Detecta si estamos en "móvil" (coincide con @media del CSS)
    function isMobile() {
        return window.matchMedia('(max-width: 992px)').matches;
    }

    // ===== Eventos =====
    if (BTN) {
        BTN.addEventListener('click', function () {
            if (isMobile()) {
                // En móvil: actúa como offcanvas (mostrar/ocultar)
                LAYOUT.classList.toggle('--sidebar-open');
            } else {
                // En escritorio: colapsa/expande (persistimos en localStorage)
                const collapsed = LAYOUT.classList.toggle('--sidebar-collapsed');
                localStorage.setItem(KEY, collapsed ? 'collapsed' : 'expanded');
            }
        });
    }

    if (OVERLAY) {
        OVERLAY.addEventListener('click', function () {
            // Clic fuera cierra el offcanvas en móvil
            LAYOUT.classList.remove('--sidebar-open');
        });
    }

    // Restaurar estado en escritorio
    function restoreDesktopState() {
        const value = localStorage.getItem(KEY);
        if (!isMobile()) {
            if (value === 'collapsed') {
                LAYOUT.classList.add('--sidebar-collapsed');
            } else {
                LAYOUT.classList.remove('--sidebar-collapsed');
            }
        } else {
            // En móvil no usamos el estado de colapso de escritorio
            LAYOUT.classList.remove('--sidebar-collapsed');
        }
    }

    // Al cargar:
    restoreDesktopState();
    window.addEventListener('resize', restoreDesktopState);
})();
