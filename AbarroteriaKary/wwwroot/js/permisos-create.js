//// wwwroot/js/permisos-create.js
//(function () {
//    'use strict';
//    if (!document.querySelector('.page-create')) return;

//    const ddlRol = document.getElementById('ddlRol');
//    const ddlModulo = document.getElementById('ddlModulo');
//    const modList = document.getElementById('modList');
//    const accList = document.getElementById('submodsList');
//    const lbl = document.getElementById('lblContexto');
//    const frm = document.getElementById('frmBulk');
//    const hidRol = document.getElementById('hidRol');
//    const hidModulo = document.getElementById('hidModulo');

//    function enforceVer(container) {
//        const ver = container.querySelector('.chk-ver');
//        const any = container.querySelector('.chk-crear').checked
//            || container.querySelector('.chk-editar').checked
//            || container.querySelector('.chk-eliminar').checked;
//        if (any) ver.checked = true;
//    }

//    // Render del acordeón con soporte de model binding fuerte:
//    //  - Items.Index por fila
//    //  - hidden "false" + checkbox "true" por cada acción
//    function renderAccordion(list) {
//        accList.innerHTML = '';
//        let idx = 0;

//        list.forEach(it => {
//            const wrap = document.createElement('div');
//            wrap.className = 'acc-item';
//            wrap.dataset.idx = idx;

//            const hd = document.createElement('button');
//            hd.type = 'button';
//            hd.className = 'acc-hd';
//            hd.innerHTML = `
//                <span class="acc-title">${it.text}</span>
//                ${it.exists ? '<span class="badge text-bg-secondary ms-2">Ya asignado</span>' : ''}
//                <i class="bi bi-chevron-down ms-auto acc-ic"></i>
//            `;
//            wrap.appendChild(hd);

//            const body = document.createElement('div');
//            body.className = 'acc-body';
//            body.hidden = true;

//            const dis = it.exists ? 'disabled' : '';

//            // IMPORTANTE:
//            //  - Items.Index
//            //  - Para cada acción: hidden "false" (después del checkbox para que
//            //    el binder tome "true" si está marcado).
//            body.innerHTML = `
//                <input type="hidden" name="Items.Index" value="${idx}">
//                <div class="ops-grid">
//                  <label class="form-check d-flex align-items-center gap-2">
//                    <input type="checkbox" class="form-check-input chk-ver" name="Items[${idx}].Ver" value="true" ${dis} />
//                    <span>Ver</span>
//                    <input type="hidden" name="Items[${idx}].Ver" value="false">
//                  </label>

//                  <label class="form-check d-flex align-items-center gap-2">
//                    <input type="checkbox" class="form-check-input chk-crear" name="Items[${idx}].Crear" value="true" ${dis} />
//                    <span>Crear</span>
//                    <input type="hidden" name="Items[${idx}].Crear" value="false">
//                  </label>

//                  <label class="form-check d-flex align-items-center gap-2">
//                    <input type="checkbox" class="form-check-input chk-editar" name="Items[${idx}].Editar" value="true" ${dis} />
//                    <span>Editar</span>
//                    <input type="hidden" name="Items[${idx}].Editar" value="false">
//                  </label>

//                  <label class="form-check d-flex align-items-center gap-2">
//                    <input type="checkbox" class="form-check-input chk-eliminar" name="Items[${idx}].Eliminar" value="true" ${dis} />
//                    <span>Eliminar</span>
//                    <input type="hidden" name="Items[${idx}].Eliminar" value="false">
//                  </label>
//                </div>

//                <!-- Campos de la fila -->
//                <input type="hidden" name="Items[${idx}].SubmoduloId" value="${it.value || ''}">
//                <input type="hidden" name="Items[${idx}].SubmoduloNombre" value="${it.text}">
//                <input type="hidden" name="Items[${idx}].YaAsignado" value="${it.exists ? 'true' : 'false'}">
//            `;

//            wrap.appendChild(body);
//            accList.appendChild(wrap);

//            hd.addEventListener('click', () => {
//                const isHidden = body.hidden;
//                body.hidden = !isHidden;
//                wrap.classList.toggle('open', isHidden);
//            });

//            ['.chk-crear', '.chk-editar', '.chk-eliminar'].forEach(sel => {
//                body.querySelector(sel).addEventListener('change', () => enforceVer(body));
//            });

//            idx++;
//        });
//    }

//    async function loadSubmods() {
//        const rolId = ddlRol.value;
//        const modId = ddlModulo.value;

//        lbl.textContent = (!rolId || !modId) ? 'Seleccione rol y módulo…' : 'Cargando…';
//        accList.innerHTML = '';
//        hidRol.value = rolId || '';
//        hidModulo.value = modId || '';

//        if (!rolId || !modId) return;

//        const url = `/Permisos/SubmodsEstado?moduloId=${encodeURIComponent(modId)}&rolId=${encodeURIComponent(rolId)}`;
//        const resp = await fetch(url);
//        if (!resp.ok) { lbl.textContent = 'Error al cargar submódulos'; return; }
//        const data = await resp.json(); // [{value,text,exists}]
//        renderAccordion(data);
//        lbl.textContent = `${ddlModulo.selectedOptions[0]?.text} • ${ddlRol.selectedOptions[0]?.text}`;
//    }

//    // Lista lateral de módulos -> selecciona módulo y carga
//    modList?.querySelectorAll('.mod-item').forEach(li => {
//        li.addEventListener('click', () => {
//            modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
//            li.classList.add('active');
//            ddlModulo.value = li.dataset.id;
//            loadSubmods();
//        });
//    });

//    //ddlRol?.addEventListener('change', loadSubmods);


//    ddlRol?.addEventListener('change', () => {
//        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;

//        if (!ddlRol.value) {
//            if (ddlModulo) ddlModulo.value = '';
//            accList.innerHTML = '';
//            lbl.textContent = 'Seleccione rol y módulo…';
//            return; // no intentes cargar aún
//        }
//        // si hay rol, puedes cargar si ya hay módulo elegido
//        if (ddlModulo?.value) loadSubmods();
//    });



//    ddlModulo?.addEventListener('change', loadSubmods);

//    // Si venís con rol preseleccionado, auto-cargar al elegir módulo de la izquierda
//    //document.addEventListener('DOMContentLoaded', () => {
//    //    if (ddlRol.value && ddlModulo.value) loadSubmods();
//    //});

//    document.addEventListener('DOMContentLoaded', () => {
//        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;
//        if (!ddlRol.value || !ddlModulo.value) {
//            accList.innerHTML = '';
//            lbl.textContent = 'Seleccione rol y módulo…';
//        }
//    });


//    // Validación antes de enviar (sin alert y sin congelar la UI)
//    frm?.addEventListener('submit', function onSubmit(e) {
//        const err = document.getElementById('errorBox');
//        const load = document.getElementById('loadState');
//        const btn = document.getElementById('btn-guardar');

//        const bail = (msg) => {
//            // Quitar cualquier estado de "cargando"
//            if (load) load.hidden = true;
//            if (btn) {
//                btn.disabled = false;
//                btn.removeAttribute('aria-busy');
//                btn.classList.remove('is-loading');
//            }
//            if (err) {
//                err.textContent = msg;
//                err.classList.remove('d-none');
//                try { err.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
//            }
//            if (window.KarySwal?.toastWarn) KarySwal.toastWarn(msg);

//            // ¡Clave! Evitar que otros handlers activen overlays/loaders
//            e.preventDefault();
//            e.stopPropagation();
//            e.stopImmediatePropagation();
//            return false;
//        };

//        // Limpia error visible
//        if (err) err.classList.add('d-none');

//        // 1) Validar rol/módulo
//        if (!ddlRol.value || !ddlModulo.value) {
//            return bail('Seleccione Rol y Módulo.');
//        }

//        // 2) Debe haber al menos una acción marcada (sin contar deshabilitadas)
//        const anyNewChecked = accList.querySelector('input[type=checkbox]:not([disabled]):checked');
//        if (!anyNewChecked) {
//            return bail('Marque al menos una acción.');
//        }

//        // 3) Todo OK: mostrar loader/deshabilitar para evitar doble envío
//        if (btn) {
//            btn.disabled = true;
//            btn.setAttribute('aria-busy', 'true');
//            btn.classList.add('is-loading');
//        }
//        if (load) load.hidden = false;
//    });

//})();

//// --- PARCHE ANTI-CONGELADO: validar en el CLICK del botón Guardar ---
//(function () {
//    const btnGuardar = document.getElementById('btn-guardar');
//    if (!btnGuardar || !frm) return;

//    const err = document.getElementById('errorBox');
//    const load = document.getElementById('loadState');

//    const showErr = (msg) => {
//        if (load) load.hidden = true;
//        if (btnGuardar) {
//            btnGuardar.disabled = false;
//            btnGuardar.removeAttribute('aria-busy');
//            btnGuardar.classList.remove('is-loading');
//        }
//        if (err) {
//            err.textContent = msg;
//            err.classList.remove('d-none');
//            try { err.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
//        }
//        if (window.KarySwal?.toastWarn) KarySwal.toastWarn(msg);
//    };

//    // Valida requisitos mínimos (rol/módulo + al menos una acción nueva)
//    const isValid = () => {
//        if (err) err.classList.add('d-none');
//        if (!ddlRol.value || !ddlModulo.value) {
//            showErr('Seleccione Rol y Módulo.');
//            return false;
//        }
//        const anyNewChecked = accList.querySelector('input[type=checkbox]:not([disabled]):checked');
//        if (!anyNewChecked) {
//            showErr('Marque al menos una acción.');
//            return false;
//        }
//        return true;
//    };

//    // Capturamos EL CLICK primero: si no es válido, no dejamos ni que empiece el submit
//    btnGuardar.addEventListener('click', function (e) {
//        if (!isValid()) {
//            e.preventDefault();
//            e.stopPropagation();
//            e.stopImmediatePropagation();
//            return false;
//        }
//        // Válido: activar loader/busy aquí para evitar doble envío
//        if (load) load.hidden = false;
//        btnGuardar.disabled = true;
//        btnGuardar.setAttribute('aria-busy', 'true');
//        btnGuardar.classList.add('is-loading');
//    }, { capture: true });

//    // Defensa extra: en submit, si algo externo lo dispara sin click
//    frm.addEventListener('submit', function (e) {
//        if (!isValid()) {
//            e.preventDefault();
//            e.stopPropagation();
//            e.stopImmediatePropagation();
//            return false;
//        }
//    }, { capture: true });
//})();


// wwwroot/js/permisos-create.js
(function () {
    'use strict';
    // Solo corre en la vista de Create
    if (!document.querySelector('.page-create')) return;

    // ---------- refs ----------
    const ddlRol = document.getElementById('ddlRol');
    const ddlModulo = document.getElementById('ddlModulo');
    const modList = document.getElementById('modList');
    const accList = document.getElementById('submodsList');
    const lbl = document.getElementById('lblContexto');
    const frm = document.getElementById('frmBulk');
    const hidRol = document.getElementById('hidRol');
    const hidModulo = document.getElementById('hidModulo');
    const btnGuardar = document.getElementById('btn-guardar');
    const errBox = document.getElementById('errorBox');
    const loadState = document.getElementById('loadState');

    // ---------- utils ----------
    function enforceVer(container) {
        const ver = container.querySelector('.chk-ver');
        const any = container.querySelector('.chk-crear').checked
            || container.querySelector('.chk-editar').checked
            || container.querySelector('.chk-eliminar').checked;
        if (any) ver.checked = true;
    }

    const stopGlobalProcessing = () => {
        if (loadState) loadState.hidden = true;
        btnGuardar?.classList.remove('is-loading');
        btnGuardar?.removeAttribute('aria-busy');
        // Por si algún overlay global quedó abierto
        try { window.KarySwal?.unblock?.(); } catch { }
        try { window.KarySwal?.stopProcessing?.(); } catch { }
        try { if (window.Swal?.isVisible?.()) window.Swal.close(); } catch { }
    };

    const showErr = (msg) => {
        stopGlobalProcessing();
        if (errBox) {
            errBox.textContent = msg;
            errBox.classList.remove('d-none');
            try { errBox.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch { }
        }
        if (window.KarySwal?.toastWarn) window.KarySwal.toastWarn(msg);
    };

    const isValid = () => {
        if (errBox) errBox.classList.add('d-none');
        if (!ddlRol.value || !ddlModulo.value) {
            showErr('Seleccione Rol y Módulo.');
            return false;
        }
        const anyNewChecked = accList.querySelector('input[type=checkbox]:not([disabled]):checked');
        if (!anyNewChecked) {
            showErr('Marque al menos una acción.');
            return false;
        }
        return true;
    };

    // ---------- render acordeón ----------
    function renderAccordion(list) {
        accList.innerHTML = '';
        let idx = 0;

        list.forEach(it => {
            const wrap = document.createElement('div');
            wrap.className = 'acc-item';
            wrap.dataset.idx = idx;

            const hd = document.createElement('button');
            hd.type = 'button';
            hd.className = 'acc-hd';
            hd.innerHTML = `
        <span class="acc-title">${it.text}</span>
        ${it.exists ? '<span class="badge text-bg-secondary ms-2">Ya asignado</span>' : ''}
        <i class="bi bi-chevron-down ms-auto acc-ic"></i>
      `;
            wrap.appendChild(hd);

            const body = document.createElement('div');
            body.className = 'acc-body';
            body.hidden = true;

            const dis = it.exists ? 'disabled' : '';

            // Hidden "false" detrás del checkbox "true" para que el binder priorice el true
            body.innerHTML = `
        <input type="hidden" name="Items.Index" value="${idx}">

        <div class="ops-grid">
          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-ver"
                   name="Items[${idx}].Ver" value="true" ${dis} />
            <span>Ver</span>
            <input type="hidden" name="Items[${idx}].Ver" value="false">
          </label>

          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-crear"
                   name="Items[${idx}].Crear" value="true" ${dis} />
            <span>Crear</span>
            <input type="hidden" name="Items[${idx}].Crear" value="false">
          </label>

          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-editar"
                   name="Items[${idx}].Editar" value="true" ${dis} />
            <span>Editar</span>
            <input type="hidden" name="Items[${idx}].Editar" value="false">
          </label>

          <label class="form-check d-flex align-items-center gap-2">
            <input type="checkbox" class="form-check-input chk-eliminar"
                   name="Items[${idx}].Eliminar" value="true" ${dis} />
            <span>Eliminar</span>
            <input type="hidden" name="Items[${idx}].Eliminar" value="false">
          </label>
        </div>

        <input type="hidden" name="Items[${idx}].SubmoduloId" value="${it.value || ''}">
        <input type="hidden" name="Items[${idx}].SubmoduloNombre" value="${it.text}">
        <input type="hidden" name="Items[${idx}].YaAsignado" value="${it.exists ? 'true' : 'false'}">
      `;

            wrap.appendChild(body);
            accList.appendChild(wrap);

            hd.addEventListener('click', () => {
                const isHidden = body.hidden;
                body.hidden = !isHidden;
                wrap.classList.toggle('open', isHidden);
            });

            ['.chk-crear', '.chk-editar', '.chk-eliminar'].forEach(sel => {
                body.querySelector(sel).addEventListener('change', () => enforceVer(body));
            });

            idx++;
        });
    }

    // ---------- carga submódulos ----------
    async function loadSubmods() {
        const rolId = ddlRol.value;
        const modId = ddlModulo.value;

        lbl.textContent = (!rolId || !modId) ? 'Seleccione rol y módulo…' : 'Cargando…';
        accList.innerHTML = '';
        hidRol.value = rolId || '';
        hidModulo.value = modId || '';

        if (!rolId || !modId) return;

        const url = `/Permisos/SubmodsEstado?moduloId=${encodeURIComponent(modId)}&rolId=${encodeURIComponent(rolId)}`;
        const resp = await fetch(url);
        if (!resp.ok) { lbl.textContent = 'Error al cargar submódulos'; return; }
        const data = await resp.json(); // [{value,text,exists}]
        renderAccordion(data);
        lbl.textContent = `${ddlModulo.selectedOptions[0]?.text} • ${ddlRol.selectedOptions[0]?.text}`;
    }

    // ---------- eventos selección ----------
    modList?.querySelectorAll('.mod-item').forEach(li => {
        li.addEventListener('click', () => {
            modList.querySelectorAll('.mod-item').forEach(x => x.classList.remove('active'));
            li.classList.add('active');
            ddlModulo.value = li.dataset.id;
            loadSubmods();
        });
    });

    ddlRol?.addEventListener('change', () => {
        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;
        if (!ddlRol.value) {
            if (ddlModulo) ddlModulo.value = '';
            accList.innerHTML = '';
            lbl.textContent = 'Seleccione rol y módulo…';
            return;
        }
        if (ddlModulo?.value) loadSubmods();
    });

    ddlModulo?.addEventListener('change', loadSubmods);

    document.addEventListener('DOMContentLoaded', () => {
        if (ddlModulo) ddlModulo.disabled = !ddlRol.value;
        if (!ddlRol.value || !ddlModulo.value) {
            accList.innerHTML = '';
            lbl.textContent = 'Seleccione rol y módulo…';
        }
    });

    // ---------- validación + control de overlays ----------
    // Click (captura): valida ANTES de que otros scripts muestren overlays globales
    btnGuardar?.addEventListener('click', (e) => {
        if (!isValid()) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            return false;
        }
        // Válido: muestra loader ligero, sin deshabilitar aquí (para no abortar el submit nativo)
        if (loadState) loadState.hidden = false;
        btnGuardar.setAttribute('aria-busy', 'true');
        btnGuardar.classList.add('is-loading');
    }, { capture: true });

    // Submit (captura): segunda línea de defensa + bloqueo final
    frm?.addEventListener('submit', (e) => {
        const ok = isValid();
        if (!ok) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            return false;
        }
        // Ahora sí, deshabilitar para evitar doble envío
        btnGuardar.disabled = true;
    }, { capture: true });

})();
