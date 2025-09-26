(function () {
    const tipoSel = document.getElementById('rpt-tipo');
    const sections = document.querySelectorAll('form#frmRpt section[data-rpt]');
    const exporters = document.querySelectorAll('#exporters > span[data-rpt]');
    const btnGen = document.getElementById('btn-generar');

    function showFor(tipo) {
        sections.forEach(s => s.hidden = (s.getAttribute('data-rpt') !== tipo));
        exporters.forEach(e => e.hidden = (e.getAttribute('data-rpt') !== tipo));
        if (tipo === 'CERRADO') { loadPedidosCerrados(); }
        if (tipo === 'COMPRAS') { setupProveedorPicker(); }
    }

    function qsFromForm() {
        const frm = document.getElementById('frmRpt');
        const visible = Array.from(sections).filter(s => !s.hidden);
        const fd = new FormData(frm);

        // Elimina campos de secciones ocultas
        Array.from(sections).filter(s => s.hidden).forEach(s => {
            s.querySelectorAll('input,select,textarea').forEach(el => fd.delete(el.name));
        });

        const p = new URLSearchParams();
        for (const [k, v] of fd.entries()) {
            if (v !== null && v !== undefined && String(v).length) {
                p.append(k, v);
            }
        }
        return p;
    }

    const targets = {
        GENERAL: { url: '/PedidosReportes/General' },
        COTIZACION: { url: '/PedidosReportes/Cotizacion' },
        COMPRAS: { url: '/PedidosReportes/ComprasProveedor' },
        CERRADO: { url: '/PedidosReportes/PedidoCerrado' }
    };

    // ========= Picker “Pedido CERRADO” =========
    let pcTimer;
    function loadPedidosCerrados() {
        const tbody = document.querySelector('#pc-tabla tbody');
        const total = document.getElementById('pc-total');
        const q = document.getElementById('pc-buscar')?.value || '';
        const params = qsFromForm();
        params.set('q', q);
        params.set('take', '50');

        fetch('/PedidosReportes/BuscarPedidosCerrados?' + params.toString())
            .then(r => r.ok ? r.json() : Promise.reject(r.status))
            .then(list => {
                tbody.innerHTML = '';
                list.forEach(row => {
                    const tr = document.createElement('tr');
                    tr.innerHTML = `
            <td class="text-center">
              <input type="radio" name="pc-sel" value="${row.pedidoId}">
            </td>
            <td>${row.pedidoId}</td>
            <td>${row.proveedor}</td>
            <td>${row.fechaPedido}</td>
            <td>${row.fechaRecibido}</td>
            <td class="text-end">${row.total}</td>`;
                    tr.addEventListener('click', (ev) => {
                        tr.querySelector('input[type="radio"]').checked = true;
                        document.getElementById('pc-pedidoId').value = row.pedidoId;
                        tbody.querySelectorAll('tr').forEach(x => x.classList.remove('selected'));
                        tr.classList.add('selected');
                        document.getElementById('pc-error').style.display = 'none';
                    });
                    tbody.appendChild(tr);
                });
                total.textContent = list.length;
            })
            .catch(_ => {
                // opcional: mostrar toast
            });
    }
    // buscador con debounce
    document.addEventListener('input', function (ev) {
        if (ev.target && ev.target.id === 'pc-buscar') {
            clearTimeout(pcTimer);
            pcTimer = setTimeout(loadPedidosCerrados, 300);
        }
    });

    // ========= Picker “Proveedor (Compras)” =========
    function setupProveedorPicker() {
        const list = document.getElementById('pv-list');
        const buscar = document.getElementById('pv-buscar');
        const hidden = document.getElementById('pv-proveedorId');
        if (!list || !buscar || !hidden) return;

        // selección
        list.addEventListener('change', function (ev) {
            const sel = list.querySelector('input[name="ProveedorSeleccion"]:checked');
            if (sel) hidden.value = sel.value || '__TODOS__';
        });

        // filtro local
        buscar.addEventListener('input', function () {
            const q = (buscar.value || '').trim().toLowerCase();
            let visibles = 0;
            list.querySelectorAll('.rpt-radio').forEach(label => {
                const isTodos = label.querySelector('input')?.value === '__TODOS__';
                if (isTodos) { label.hidden = false; visibles++; return; }
                const name = (label.getAttribute('data-name') || '');
                const show = q.length === 0 || name.indexOf(q) >= 0;
                label.hidden = !show;
                if (show) visibles++;
            });
            document.getElementById('pv-total').textContent = String(visibles);
        });
    }

    // ========= Generar =========
    btnGen.addEventListener('click', function () {
        const tipo = tipoSel.value;
        const tgt = targets[tipo];
        if (!tgt) return;

        if (tipo === 'CERRADO') {
            const pedidoId = document.getElementById('pc-pedidoId')?.value || '';
            if (!pedidoId) {
                document.getElementById('pc-error').style.display = 'block';
                if (window.KarySwal?.alert) KarySwal.alert({ title: 'Seleccione un pedido', icon: 'warning' });
                return;
            }
        }

        // COMPRAS: ProveedorId puede ser "__TODOS__"
        if (tipo === 'COMPRAS') {
            // nada especial: se va en el formulario como hidden
        }

        const url = tgt.url + '?' + qsFromForm().toString();
        window.location.href = url;
    });

    // init
    showFor(tipoSel.value);
    tipoSel.addEventListener('change', () => showFor(tipoSel.value));
})();
