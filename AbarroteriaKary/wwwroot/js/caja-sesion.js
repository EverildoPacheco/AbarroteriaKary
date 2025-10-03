// wwwroot/js/caja-sesion.js
(function () {
    'use strict';

    // Evitar doble init
    if (window.__cajaSesionInit) return;
    window.__cajaSesionInit = true;

    const host = document.getElementById('ak-modal-host');
    const btnNueva = document.getElementById('btnNuevaVenta');
    const btnOpen = document.getElementById('btnAperturarCaja');
    const btnClose = document.getElementById('btnCerrarCaja');

    function getCajaId() {
        return document.getElementById('CajaIdActual')?.value || '';
    }

    function setNuevaVentaEnabled(abierta) {
        if (!btnNueva) return;
        if (abierta) {
            btnNueva.classList.remove('disabled');
            btnNueva.removeAttribute('aria-disabled');
        } else {
            btnNueva.classList.add('disabled');
            btnNueva.setAttribute('aria-disabled', 'true');
        }
    }

    // Sincroniza UI cuando se emite el evento
    document.addEventListener('caja:estadoChanged', (ev) => {
        const abierta = !!(ev.detail && ev.detail.abierta);
        setNuevaVentaEnabled(abierta);
        window.__cajaSesionId = ev.detail?.sesionId || null;
    });

    async function getEstado(cajaId) {
        const rsp = await fetch(`/CajaSesion/Estado?cajaId=${encodeURIComponent(cajaId)}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        try { return await rsp.json(); } catch { return { ok: false }; }
    }

    // Inserta modal, elimina uno anterior con el mismo id y lo muestra
    function insertAndShowModal(html, modalId) {
        if (!host) return;

        const prev = document.getElementById(modalId);
        if (prev && prev.parentElement) prev.parentElement.removeChild(prev);

        const wrap = document.createElement('div');
        wrap.innerHTML = html;

        const modalEl = wrap.querySelector('#' + modalId);
        if (!modalEl) return;

        host.appendChild(modalEl);

        try {
            const bsModal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
            bsModal.show();
        } catch (e) {
            console.error('Bootstrap modal error:', e);
        }
    }

    async function loadPartial(url) {
        const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        const ct = (res.headers.get('Content-Type') || '').toLowerCase();
        if (!res.ok) {
            if (ct.includes('application/json')) {
                const js = await res.json().catch(() => ({}));
                throw new Error(js.message || 'Error de servidor.');
            }
            throw new Error('Error de servidor.');
        }
        return await res.text();
    }

    // ---------- Botón: APERTURAR (GET /CajaSesion/Abrir?cajaId=...) ----------
    if (btnOpen && !btnOpen.dataset.bound) {
        btnOpen.dataset.bound = '1';
        btnOpen.addEventListener('click', async (ev) => {
            ev.preventDefault();
            const cajaId = getCajaId();
            if (!cajaId) {
                if (window.KarySwal?.error) KarySwal.error({ title: 'Caja no definida', text: 'No se indicó la caja.' });
                return;
            }

            // Si ya está abierta, avisar
            try {
                const st = await getEstado(cajaId);
                if (st?.ok && st.estado === 'ABIERTA') {
                    if (window.KarySwal?.info) KarySwal.info({ title: 'Caja ya aperturada', text: 'Cierre la caja actual antes de abrir otra sesión.' });
                    return;
                }
            } catch { /* continúa */ }

            try {
                const html = await loadPartial(`/CajaSesion/Abrir?cajaId=${encodeURIComponent(cajaId)}`);
                insertAndShowModal(html, 'modalCajaApertura');
            } catch (e) {
                if (window.KarySwal?.error) KarySwal.error({ title: 'No se pudo cargar', text: e.message || '' });
            }
        });
    }

    // ---------- Botón: CERRAR (GET /CajaSesion/Cerrar?cajaId=...) ----------
    if (btnClose && !btnClose.dataset.bound) {
        btnClose.dataset.bound = '1';
        btnClose.addEventListener('click', async (ev) => {
            ev.preventDefault();
            const cajaId = getCajaId();
            if (!cajaId) {
                if (window.KarySwal?.error) KarySwal.error({ title: 'Caja no definida', text: 'No se indicó la caja.' });
                return;
            }

            // Solo si está ABIERTA
            try {
                const st = await getEstado(cajaId);
                if (!(st?.ok && st.estado === 'ABIERTA')) {
                    if (window.KarySwal?.info) KarySwal.info({ title: 'No hay caja aperturada', text: 'Aperture una caja antes de cerrar.' });
                    return;
                }
            } catch {
                if (window.KarySwal?.error) KarySwal.error({ title: 'Error', text: 'No se pudo consultar el estado de caja.' });
                return;
            }

            try {
                const html = await loadPartial(`/CajaSesion/Cerrar?cajaId=${encodeURIComponent(cajaId)}`);
                insertAndShowModal(html, 'modalCajaCierre');
            } catch (e) {
                if (window.KarySwal?.error) KarySwal.error({ title: 'No se pudo cargar', text: e.message || '' });
            }
        });
    }

    // ---------- Delegación SUBMIT (POST Abrir / Cerrar) ----------
    document.addEventListener('submit', async function (ev) {
        const form = ev.target;
        if (!(form instanceof HTMLFormElement)) return;

        // APERTURA -> POST /CajaSesion/Abrir
        if (form.id === 'form-apertura-caja') {
            ev.preventDefault();
            const data = new FormData(form);

            try { window.KaryForms?.lockButton?.('#btn-aperturar', '<i class="fa-solid fa-spinner fa-spin me-1"></i> Abriendo...'); } catch { }

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    body: data,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const json = await res.json();

                if (!json.ok) {
                    if (window.KarySwal?.error) KarySwal.error({ title: 'No se pudo abrir la caja', text: json.message || '' });
                    return;
                }

                if (window.KarySwal?.saveSuccess) {
                    KarySwal.saveSuccess({ title: '¡Caja aperturada!', text: json.message || 'La caja quedó abierta.' });
                }

                document.dispatchEvent(new CustomEvent('caja:estadoChanged', { detail: { abierta: true, sesionId: json.sesionId } }));

                const modalEl = document.getElementById('modalCajaApertura');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal?.hide();
                modalEl?.remove();
            } catch (e) {
                if (window.KarySwal?.error) KarySwal.error({ title: 'Error', text: e.message || 'No se pudo abrir la caja.' });
            } finally {
                try { window.KaryForms?.unlockButton?.('#btn-aperturar'); } catch { }
            }
        }

        // CIERRE -> POST /CajaSesion/Cerrar
        if (form.id === 'form-cierre-caja') {
            ev.preventDefault();
            const data = new FormData(form);

            try { window.KaryForms?.lockButton?.('#btn-cerrar', '<i class="fa-solid fa-spinner fa-spin me-1"></i> Cerrando...'); } catch { }

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    body: data,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const json = await res.json();

                if (!json.ok) {
                    if (window.KarySwal?.error) KarySwal.error({ title: 'No se pudo cerrar la caja', text: json.message || '' });
                    return;
                }

                if (window.KarySwal?.saveSuccess) {
                    KarySwal.saveSuccess({ title: '¡Caja cerrada!', text: json.message || 'La caja quedó cerrada.' });
                }

                document.dispatchEvent(new CustomEvent('caja:estadoChanged', { detail: { abierta: false, sesionId: null } }));

                const modalEl = document.getElementById('modalCajaCierre');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal?.hide();
                modalEl?.remove();
            } catch (e) {
                if (window.KarySwal?.error) KarySwal.error({ title: 'Error', text: e.message || 'No se pudo cerrar la caja.' });
            } finally {
                try { window.KaryForms?.unlockButton?.('#btn-cerrar'); } catch { }
            }
        }
    });

    // Estado inicial (habilita/deshabilita "Nueva venta")
    (async function initEstado() {
        const cajaId = getCajaId();
        if (!cajaId) return;
        try {
            const st = await getEstado(cajaId);
            const abierta = st?.ok && st.estado === 'ABIERTA';
            document.dispatchEvent(new CustomEvent('caja:estadoChanged', {
                detail: { abierta, sesionId: abierta ? st.sesionId : null }
            }));
        } catch { /* silencioso */ }
    })();
})();
