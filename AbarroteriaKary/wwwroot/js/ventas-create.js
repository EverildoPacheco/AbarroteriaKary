(function () {
    'use strict';

    const fmtGT = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });
    const body = document.getElementById('detalleBody');
    const totalC = document.getElementById('totalCell');

    // ------------- Utils -------------
    const debounce = (fn, ms = 250) => {
        let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
    };
    const parseNum = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };

    function recalcRow(tr) {
        const qty = parseNum(tr.querySelector('.js-cant').value);
        const pvp = parseNum(tr.querySelector('.js-pvp').value);
        const sub = Math.round(qty * pvp * 100) / 100;
        tr.querySelector('.subtotal-cell').textContent = fmtGT.format(sub);
        return sub;
    }
    function recalcTotal() {
        let total = 0;
        body.querySelectorAll('tr').forEach(tr => total += recalcRow(tr));
        totalC.textContent = fmtGT.format(Math.round(total * 100) / 100);
        return total;
    }
    function reindexDetalle() {
        body.querySelectorAll('tr').forEach((tr, i) => {
            tr.dataset.idx = i;
            tr.querySelectorAll('[name^="Lineas["]').forEach(inp => {
                const prop = inp.getAttribute('name').replace(/^Lineas\[\d+\]\./, '');
                inp.setAttribute('name', `Lineas[${i}].${prop}`);
            });
        });
    }
    function findRowByProducto(productoId) {
        return body.querySelector(`tr[data-producto-id="${productoId}"]`);
    }

    // ------------- Add / Merge -------------
    function addOrMergeLinea({ productoId, codigo, nombre, imagenUrl, pvp, cantidad }) {
        const exists = findRowByProducto(productoId);
        if (exists) {
            const qtyInp = exists.querySelector('.js-cant');
            qtyInp.value = parseNum(qtyInp.value) + cantidad;
            recalcTotal();
            return;
        }

        const idx = body.querySelectorAll('tr').length;
        const tr = document.createElement('tr');
        tr.dataset.idx = idx;
        tr.dataset.productoId = productoId;

        tr.innerHTML = `
      <td class="text-monospace">
        <input type="hidden" name="Lineas[${idx}].ProductoId" value="${productoId}">
        <input type="hidden" name="Lineas[${idx}].CodigoProducto" value="${codigo ?? ''}">
        <input type="hidden" name="Lineas[${idx}].NombreProducto" value="${nombre ?? ''}">
        <input type="hidden" name="Lineas[${idx}].ImagenUrl" value="${imagenUrl ?? ''}">
        ${codigo || productoId}
      </td>
      <td>${nombre ?? ''}</td>
      <td class="text-center">
        ${imagenUrl ? `<img src="${imagenUrl}" onerror="this.src='/img/no-image.png'" class="ak-thumb" alt="ref">` : '-'}
      </td>
      <td class="text-end">
        <input type="number" min="1" step="1" class="form-control text-end js-cant"
               name="Lineas[${idx}].Cantidad" value="${cantidad}">
      </td>
      <td class="text-end">
        <input type="number" min="0" step="0.01" class="form-control text-end js-pvp ak-readonly"
               name="Lineas[${idx}].PrecioUnitario" value="${pvp}" readonly>
      </td>
      <td class="text-end subtotal-cell"></td>
      <td class="text-center">
        <button type="button" class="btn btn-sm btn-danger js-del"><i class="fa-solid fa-trash"></i></button>
      </td>`;
        body.appendChild(tr);
        recalcTotal();
    }

    // ========== Autocompletar Genérico ==========
    function bindSuggest(inputId, listId, fetcher, onPick) {
        const inp = document.getElementById(inputId);
        const list = document.getElementById(listId);
        if (!inp || !list) return;

        const render = items => {
            if (!items || !items.length) { list.classList.remove('show'); list.innerHTML = ''; return; }
            list.innerHTML = items.map(it => it.html).join('');
            list.classList.add('show');
            // clicks
            list.querySelectorAll('[data-pick]').forEach(a => {
                a.addEventListener('click', ev => {
                    ev.preventDefault();
                    onPick(JSON.parse(a.getAttribute('data-pick')));
                    list.classList.remove('show');
                    list.innerHTML = '';
                });
            });
        };

        const doSearch = debounce(async () => {
            const q = (inp.value || '').trim();
            if (q.length < 2) { render([]); return; }
            const url = inp.dataset.url + `?q=${encodeURIComponent(q)}`;
            try {
                const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const js = await rsp.json();
                if (!js.ok) { render([]); return; }
                render(js.items.map(m => ({ html: m._html, data: m })));
            } catch { render([]); }
        }, 250);

        inp.addEventListener('input', doSearch);
        inp.addEventListener('focus', doSearch);
        document.addEventListener('click', e => {
            if (!list.contains(e.target) && e.target !== inp) { list.classList.remove('show'); }
        });
    }

    // ----- Clientes -----
    bindSuggest('txtCliente', 'sugClientes', null, (item) => {
        // item: {clienteId, nombre, nit, info}
        document.getElementById('ClienteId').value = item.clienteId;
        document.getElementById('txtCliente').value = item.nombre;
        document.getElementById('txtNitCliente').value = item.nit || '';
    });

    // ----- Productos -----
    bindSuggest('txtProducto', 'sugProductos', null, async (item) => {
        // item: {productoId, ...}
        await abrirModalProducto(item.productoId);
        document.getElementById('txtProducto').value = '';
    });

    // ========== Modal parcial de producto ==========
    // ---------- Abrir modal (parcial Razor) ----------
    async function abrirModalProducto(productoId) {
        // Elimina modal previo si existiera (evita listeners duplicados)
        document.getElementById('modalAgregarProducto')?.remove();
        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('padding-right');

        // Pide el PARCIAL del modal
        let html;
        try {
            const rsp = await fetch(`/Ventas/AgregarProductoModal?productoId=${encodeURIComponent(productoId)}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!rsp.ok) throw new Error(await rsp.text());
            html = await rsp.text();
        } catch (e) {
            (window.KarySwal?.error ?? alert)({ title: 'Error', text: e.message || 'No se pudo abrir el modal.' });
            return;
        }

        // Inyecta y muestra
        const host = document.getElementById('ak-modal-host') || document.body;
        host.innerHTML = html;

        const modalEl = document.getElementById('modalAgregarProducto');
        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
        modal.show();

        // Click del botón (NO submit)
        const btnAdd = modalEl.querySelector('#btnModalAdd');
        btnAdd.addEventListener('click', () => {
            const cant = parseInt(modalEl.querySelector('#apCantidad').value, 10) || 0;
            const stock = parseFloat(modalEl.querySelector('#apStock')?.value || '0');

            if (cant <= 0) {
                (window.KarySwal?.warn ?? alert)({ title: 'Cantidad inválida' });
                return;
            }
            if (stock > 0 && cant > stock) {
                (window.KarySwal?.warn ?? alert)({ title: 'Cantidad supera el stock disponible' });
                return;
            }

            addOrMergeLinea({
                productoId: modalEl.querySelector('#apProdId').value,
                codigo: modalEl.querySelector('#apCodigo').value,
                nombre: modalEl.querySelector('#apNombre').value,
                imagenUrl: modalEl.querySelector('#apImagen').value || '/img/no-image.png',
                pvp: parseFloat(modalEl.querySelector('#apPvp').value) || 0,
                cantidad: cant
            });

            modal.hide();
        }, { once: true });

        // Limpieza al cerrar (quita backdrop colgado)
        modalEl.addEventListener('hidden.bs.modal', () => {
            modal.dispose();
            modalEl.remove();
            document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('padding-right');
        }, { once: true });
    }























    // --------- Eventos de la tabla ---------
    body.addEventListener('input', e => { if (e.target.matches('.js-cant')) recalcTotal(); });
    body.addEventListener('click', e => {
        const btn = e.target.closest('.js-del'); if (!btn) return;
        btn.closest('tr')?.remove(); reindexDetalle(); recalcTotal();
    });

    // --------- Cobrar ---------

    // --- calcular total actual (reutiliza tu función si ya la tienes) ---
    function vc_recalcTotal() {
        const fmt = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });
        let total = 0;
        document.querySelectorAll('#detalleBody tr').forEach(tr => {
            const qty = parseFloat(tr.querySelector('.js-cant')?.value || '0');
            const pvp = parseFloat(tr.querySelector('.js-pvp')?.value || '0');
            total += (Math.round(qty * pvp * 100) / 100);
        });
        document.getElementById('totalCell').textContent = fmt.format(Math.round(total * 100) / 100);
        return Math.round(total * 100) / 100;
    }

    //async function abrirModalPago(total) {
    //    // Limpieza de modal/backdrops previos
    //    document.getElementById('modalPagoVenta')?.remove();
    //    document.querySelectorAll('.modal-backdrop').forEach(el => el.remove());
    //    document.body.classList.remove('modal-open');
    //    document.body.style.removeProperty('padding-right');

    //    // Carga parcial
    //    const url = `/Ventas/PagoModal?total=${encodeURIComponent(total)}`;
    //    const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    //    if (!rsp.ok) {
    //        const txt = await rsp.text();
    //        (window.KarySwal?.error ?? alert)({ title: 'Error', text: txt || 'No se pudo cargar el modal de pago.' });
    //        return;
    //    }
    //    const html = await rsp.text();

    //    // Inyecta y muestra
    //    const host = document.getElementById('ak-modal-host') || document.body;
    //    host.insertAdjacentHTML('beforeend', html);

    //    const modalEl = document.getElementById('modalPagoVenta');
    //    const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
    //    modal.show();

    //    // --- WIRING dentro del modal ---
    //    const fmtGT = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });
    //    const parseN = v => { const n = parseFloat(String(v).replace(',', '.')); return isNaN(n) ? 0 : n; };

    //    const $totalN = parseN(modalEl.querySelector('#pvTotalNumber')?.value || '0');
    //    const $met = modalEl.querySelector('#pvMetodo');
    //    const $efec = modalEl.querySelector('#pvEfectivo');
    //    const $camb = modalEl.querySelector('#pvCambio');
    //    const $ok = modalEl.querySelector('#pvConfirmar');

    //    // Calcula cambio (auto)
    //    const calcCambio = () => {
    //        const recibido = parseN($efec.value);
    //        const cambio = Math.max(0, Math.round((recibido - $totalN) * 100) / 100);
    //        $camb.value = fmtGT.format(cambio);
    //    };
    //    calcCambio();
    //    $efec.addEventListener('input', calcCambio);

    //    // Confirmar venta
    //    $ok.addEventListener('click', () => {
    //        if (!$met.value) { $met.classList.add('is-invalid'); return; }
    //        $met.classList.remove('is-invalid');

    //        const recibido = parseN($efec.value);
    //        if (recibido < $totalN) { $efec.classList.add('is-invalid'); return; }
    //        $efec.classList.remove('is-invalid');

    //        // Llenar hidden del form principal
    //        document.getElementById('MetodoPagoId').value = $met.value;
    //        document.getElementById('EfectivoRecibido').value = recibido.toFixed(2);
    //        document.getElementById('CambioCalculado').value = Math.max(0, recibido - $totalN).toFixed(2);

    //        modal.hide();
    //        modalEl.addEventListener('hidden.bs.modal', () => {
    //            modal.dispose();
    //            modalEl.remove();
    //            // Enviar form principal
    //            document.getElementById('form-venta').submit();
    //        }, { once: true });
    //    }, { once: false });
    //}


    // === Reemplazo de abrirModalPago(total) ===
    async function abrirModalPago(total) {
        // Quita modal previo para evitar handlers duplicados
        document.getElementById('modalPagoVenta')?.remove();
        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('padding-right');

        // Carga el parcial
        const url = `/Ventas/PagoModal?total=${encodeURIComponent(total)}`;
        const rsp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!rsp.ok) { (window.KarySwal?.error ?? alert)({ title: 'Error', text: 'No se pudo abrir el modal de pago.' }); return; }
        const html = await rsp.text();

        // Inyecta
        const host = document.getElementById('ak-modal-host') || document.body;
        host.insertAdjacentHTML('beforeend', html);

        // Inicializa modal
        const modalEl = document.getElementById('modalPagoVenta');
        const modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
        modal.show();

        // ---- Bind de eventos (aquí sí corre el JS) ----
        const fmtGT = new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' });
        const parseNum = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };

        const totalN = parseNum(document.getElementById('pvTotalNumber').value || '0');
        const $met = document.getElementById('pvMetodo');
        const $efec = document.getElementById('pvEfectivo');
        const $camb = document.getElementById('pvCambio');
        const $okBtn = document.getElementById('pvConfirmar');

        function calcCambio() {
            const recibido = parseNum($efec.value);
            const cambio = Math.max(0, Math.round((recibido - totalN) * 100) / 100);
            $camb.value = fmtGT.format(cambio);
        }
        calcCambio();
        $efec.addEventListener('input', calcCambio);

        async function askConfirm({ title, text, confirmText = 'Sí, registrar', cancelText = 'No' }) {
            if (window.KarySwal?.confirm) {
                return await KarySwal.confirm({ title, text, confirmText, cancelText, icon: 'question' });
            }
            if (window.Swal?.fire) {
                const r = await Swal.fire({
                    title, text, icon: 'question',
                    showCancelButton: true, confirmButtonText: confirmText, cancelButtonText: cancelText, reverseButtons: true
                });
                return r.isConfirmed;
            }
            return window.confirm(`${title}\n\n${text}`);
        }

        let submitting = false;
        $okBtn.addEventListener('click', async () => {
            if (submitting) return;

            if (!$met.value) { $met.classList.add('is-invalid'); return; }
            $met.classList.remove('is-invalid');

            const recibido = parseNum($efec.value);
            if (recibido < totalN) { $efec.classList.add('is-invalid'); return; }
            $efec.classList.remove('is-invalid');

            const cambio = Math.max(0, Math.round((recibido - totalN) * 100) / 100);
            const metodoTx = $met.options[$met.selectedIndex]?.text || $met.value;

            const ok = await askConfirm({
                title: '¿Registrar la venta?',
                text:
                    `Total: ${fmtGT.format(totalN)}\n` +
                    `Método: ${metodoTx}\n` +
                    `Efectivo: ${fmtGT.format(recibido)}\n` +
                    `Cambio: ${fmtGT.format(cambio)}`
            });
            if (!ok) return;

            // Pasa datos al form principal y envía
            document.getElementById('MetodoPagoId').value = $met.value;
            document.getElementById('EfectivoRecibido').value = recibido.toFixed(2);
            document.getElementById('CambioCalculado').value = cambio.toFixed(2);

            submitting = true;
            bootstrap.Modal.getInstance(modalEl)?.hide();
            document.getElementById('form-venta').submit();
        });

        // Limpieza al cerrar
        modalEl.addEventListener('hidden.bs.modal', () => {
            modal.dispose();
            modalEl.remove();
            document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('padding-right');
        }, { once: true });
    }



















    const btnCobrar = document.getElementById('btnCobrar');
    if (btnCobrar && !btnCobrar.dataset.wired) {
        btnCobrar.dataset.wired = '1';
        btnCobrar.addEventListener('click', async () => {
            // Evita prompts viejos y valida
            if (!document.querySelector('#detalleBody tr')) {
                (window.KarySwal?.warn ?? alert)({ title: 'Agregue productos a la venta.' });
                return;
            }
            const total = vc_recalcTotal();
            if (total <= 0) {
                (window.KarySwal?.warn ?? alert)({ title: 'Agregue productos a la venta.' });
                return;
            }
            await abrirModalPago(total);
        });
    }





    // init
    recalcTotal();




    function ajustarAlturaDetalle(rows = 5) {
        const cont = document.querySelector('.ak-table-scroll');
        if (!cont) return;
        const thead = cont.querySelector('thead tr');
        const tfoot = cont.querySelector('tfoot tr');
        const sample = cont.querySelector('tbody tr');

        const hHead = thead ? thead.getBoundingClientRect().height : 48;
        const hFoot = tfoot ? tfoot.getBoundingClientRect().height : 48;
        const hRow = sample ? sample.getBoundingClientRect().height : 52;

        cont.style.maxHeight = (hHead + rows * hRow + hFoot) + 'px';
    }











   





















})();
