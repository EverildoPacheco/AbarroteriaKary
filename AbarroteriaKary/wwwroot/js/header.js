// ====== Menú de usuario (avatar) ======
(function () {
    const btn = document.getElementById('btnUserMenu');
    const menu = document.getElementById('userMenu');
    if (!btn || !menu) return;

    const openMenu = () => { menu.hidden = false; btn.setAttribute('aria-expanded', 'true'); };
    const closeMenu = () => { menu.hidden = true; btn.setAttribute('aria-expanded', 'false'); };
    const isOpen = () => !menu.hidden;

    btn.addEventListener('click', function (e) {
        e.stopPropagation();
        isOpen() ? closeMenu() : openMenu();
    });

    // Cerrar al hacer clic fuera
    document.addEventListener('click', function (e) {
        if (!isOpen()) return;
        const within = menu.contains(e.target) || btn.contains(e.target);
        if (!within) closeMenu();
    });

    // Cerrar con tecla Esc
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && isOpen()) closeMenu();
    });
})();
