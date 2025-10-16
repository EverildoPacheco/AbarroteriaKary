(function () {
    'use strict';

    const lista = document.getElementById('listaRoles');
    const txt = document.getElementById('txtBuscarRol');
    const panel = document.getElementById('detPermisos');
    const lbl = document.getElementById('lblRolSel');

    // Filtro client-side de roles
    txt?.addEventListener('input', () => {
        const val = (txt.value || '').trim().toLowerCase();
        lista.querySelectorAll('.role-item').forEach(li => {
            const name = li.textContent.trim().toLowerCase();
            li.style.display = name.includes(val) ? '' : 'none';
        });
    });

    // Cargar detalle por AJAX al hacer click (con fallback a href si no hay fetch)
    lista?.querySelectorAll('.role-item').forEach(li => {
        li.addEventListener('click', async (ev) => {
            ev.preventDefault();
            const rolId = li.dataset.id;
            if (!rolId) return;

            // marca activo
            lista.querySelectorAll('.role-item').forEach(x => x.classList.remove('active'));
            li.classList.add('active');

            // spinner
            panel.innerHTML = `<div class="p-4 text-muted"><div class="spinner-border spinner-border-sm me-2"></div>Cargando…</div>`;

            // pushState para que se vea ?rolId=... (y SSR si refrescan)
            const url = new URL(window.location.href);
            url.searchParams.set('rolId', rolId);
            window.history.replaceState({}, '', url);

            try {
                const resp = await fetch(`/Permisos/PermisosPorRol?rolId=${encodeURIComponent(rolId)}`);
                if (!resp.ok) throw new Error('HTTP ' + resp.status);
                const html = await resp.text();
                panel.innerHTML = html;

                // Actualiza título
                const nombre = li.querySelector('span')?.textContent?.trim() || rolId;
                lbl.textContent = `Rol: ${nombre}`;
            } catch {
                panel.innerHTML = `<div class="p-4 text-danger">Error al cargar permisos.</div>`;
            }
        }, { passive: true });
    });

})();
