// Evitar inicializaciones repetidas si el layout reinyecta el script
if (!window.__notifsInit) {
    window.__notifsInit = true;

    (function () {
        const btn = document.getElementById('btnNotif');
        const badge = document.getElementById('notifBadge');
        const panel = document.getElementById('notifDropdown');
        if (!btn || !badge || !panel) return;

        let isLoadingCount = false;
        let isLoadingDropdown = false;

        async function refreshCount(force = false) {
            if (isLoadingCount) return;
            if (!force && document.visibilityState !== 'visible') return; // no molestar en 2º plano
            isLoadingCount = true;
            try {
                const r = await fetch('/Notificaciones/Count', {
                    cache: 'no-store',
                    headers: { 'X-No-Loading': '1' }
                });
                if (r.ok) {
                    const data = await r.json();
                    const n = Number(data?.count ?? 0);
                    if (n > 0) { badge.textContent = n; badge.hidden = false; }
                    else { badge.hidden = true; }
                }
            } catch { /* silenciar */ }
            finally { isLoadingCount = false; }
        }

        async function loadDropdown() {
            if (isLoadingDropdown) return;
            isLoadingDropdown = true;
            panel.innerHTML = '<div class="p-3 text-muted small">Cargando…</div>';
            try {
                const r = await fetch('/Notificaciones/Dropdown?top=10', {
                    cache: 'no-store',
                    headers: { 'X-No-Loading': '1' }
                });
                const html = await r.text();
                panel.innerHTML = html;
            } catch {
                panel.innerHTML = '<div class="p-3 text-danger small">No se pudo cargar las notificaciones.</div>';
            } finally {
                isLoadingDropdown = false;
            }
        }

        function openDropdown() { panel.hidden = false; loadDropdown(); }
        function closeDropdown() { panel.hidden = true; }

        // Toggle campana
        btn.addEventListener('click', (ev) => {
            ev.preventDefault();
            panel.hidden ? openDropdown() : closeDropdown();
        });

        // Cerrar si haces click fuera
        document.addEventListener('click', (e) => {
            if (panel.hidden) return;
            if (!panel.contains(e.target) && e.target !== btn) closeDropdown();
        });

        // Cerrar con ESC
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && !panel.hidden) closeDropdown();
        });

        // Delegación interna (un solo handler permanente)
        panel.addEventListener('click', async (ev) => {
            // 1) Recordarme luego (snooze explícito)
            const snooze = ev.target.closest('.js-notif-snooze');
            if (snooze) {
                ev.preventDefault();
                const id = snooze.getAttribute('data-id');
                const mins = parseInt(snooze.getAttribute('data-mins') || '720', 10); // 12 h por defecto
                if (id) {
                    await fetch(`/Notificaciones/Snooze/${id}?mins=${mins}`, {
                        method: 'POST',
                        headers: { 'X-No-Loading': '1' }
                    });
                    snooze.closest('.ak-notif-item')?.remove();
                    await refreshCount(true);
                    if (!panel.querySelector('.ak-notif-item')) {
                        panel.innerHTML = '<div class="text-center text-muted p-3 small">Sin notificaciones.</div>';
                    }
                }
                return;
            }

            // 2) Marcar como leída (equivale a snooze 12 h)
            const btnRead = ev.target.closest('.js-notif-read');
            if (btnRead) {
                ev.preventDefault();
                const id = btnRead.getAttribute('data-id');
                if (!id) return;
                await fetch(`/Notificaciones/Read/${id}`, {
                    method: 'POST',
                    headers: { 'X-No-Loading': '1' }
                });
                btnRead.closest('.ak-notif-item')?.remove();
                await refreshCount(true);
                if (!panel.querySelector('.ak-notif-item')) {
                    panel.innerHTML = '<div class="text-center text-muted p-3 small">Sin notificaciones.</div>';
                }
                return;
            }

            // 3) Marcar todas como leídas (12 h)
            const btnAll = ev.target.closest('.js-notif-readall');
            if (btnAll) {
                ev.preventDefault();
                await fetch('/Notificaciones/ReadAll', {
                    method: 'POST',
                    headers: { 'X-No-Loading': '1' }
                });
                panel.innerHTML = '<div class="text-center text-muted p-3 small">Sin notificaciones.</div>';
                await refreshCount(true);
                return;
            }
        });

        // Primera carga + cada 60s (solo si visible)
        refreshCount(true);
        setInterval(refreshCount, 60000);
        document.addEventListener('visibilitychange', () => refreshCount());
    })();
}








//// Evitar inicializaciones repetidas si el layout reinyecta el script
//if (!window.__notifsInit) {
//    window.__notifsInit = true;

//    (function () {
//        const btn = document.getElementById('btnNotif');
//        const badge = document.getElementById('notifBadge');
//        const panel = document.getElementById('notifDropdown');
//        if (!btn || !badge || !panel) return;

//        async function refreshCount() {
//            if (document.visibilityState !== 'visible') return; // no molestar en segundo plano
//            try {
//                const r = await fetch('/Notificaciones/Count', {
//                    cache: 'no-store',
//                    headers: { 'X-No-Loading': '1' }
//                });
//                if (!r.ok) return;
//                const data = await r.json();
//                const n = Number(data?.count ?? 0);
//                if (n > 0) { badge.textContent = n; badge.hidden = false; }
//                else { badge.hidden = true; }
//            } catch { }
//        }

//        async function loadDropdown() {
//            panel.innerHTML = '<div class="p-3 text-muted small">Cargando…</div>';
//            try {
//                const r = await fetch('/Notificaciones/Dropdown?top=10', {
//                    cache: 'no-store',
//                    headers: { 'X-No-Loading': '1' }
//                });
//                const html = await r.text();
//                panel.innerHTML = html;
//            } catch {
//                panel.innerHTML = '<div class="p-3 text-danger small">No se pudo cargar las notificaciones.</div>';
//            }
//        }

//        function openDropdown() { panel.hidden = false; loadDropdown(); }
//        function closeDropdown() { panel.hidden = true; }

//        // Toggle campana
//        btn.addEventListener('click', (ev) => {
//            ev.preventDefault();
//            panel.hidden ? openDropdown() : closeDropdown();
//        });

//        // Cerrar si haces click fuera
//        document.addEventListener('click', (e) => {
//            if (panel.hidden) return;
//            if (!panel.contains(e.target) && e.target !== btn) closeDropdown();
//        });

//        // Delegación interna (un solo handler permanente)
//        panel.addEventListener('click', async (ev) => {
//            const btnRead = ev.target.closest('.js-notif-read');
//            const btnAll = ev.target.closest('.js-notif-readall');

//            if (btnRead) {
//                ev.preventDefault();
//                const id = btnRead.getAttribute('data-id');
//                if (!id) return;
//                await fetch(`/Notificaciones/Read/${id}`, { method: 'POST', headers: { 'X-No-Loading': '1' } });
//                btnRead.closest('.ak-notif-item')?.remove();
//                await refreshCount();
//                if (!panel.querySelector('.ak-notif-item')) {
//                    panel.innerHTML = '<div class="text-center text-muted p-3 small">Sin notificaciones.</div>';
//                }
//            } else if (btnAll) {
//                ev.preventDefault();
//                await fetch('/Notificaciones/ReadAll', { method: 'POST', headers: { 'X-No-Loading': '1' } });
//                panel.innerHTML = '<div class="text-center text-muted p-3 small">Sin notificaciones.</div>';
//                await refreshCount();
//            }
//        });

//        // Primera carga + cada 60s (solo si visible)
//        refreshCount();
//        setInterval(refreshCount, 60000);
//        document.addEventListener('visibilitychange', refreshCount);
//    })();
//}
