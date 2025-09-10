// ak-sort.js  (Abarrotería Kary)
// Ordenamiento por columna (cliente) con íconos por TIPO de dato.
// - Marque <table class="js-sortable">.
// - En cada <th> ordenable ponga data-type: "text" | "number" | "date:dd/MM/yyyy".
// - Puede sobreescribir íconos por columna con:
//     data-icon-neutral="..." data-icon-asc="..." data-icon-desc="..."
// - Para celdas con valor real (badges/íconos), use data-sort-value en el <td>.

(function () {
    'use strict';

    // ===== Defaults de íconos por TIPO =====
    // TEXT => Bootstrap Icons (A–Z)
    // NUMBER/DATE => Font Awesome (flechas)
    const ICONS_DEFAULTS = {
        text: {
            neutral: 'bi bi-arrow-down-up',
            asc: 'bi bi-sort-alpha-up',
            desc: 'bi bi-sort-alpha-down'
        },
        number: {
            neutral: 'fa-solid fa-sort',
            asc: 'fa-solid fa-sort-up',
            desc: 'fa-solid fa-sort-down'
        },
        date: {
            neutral: 'fa-solid fa-sort',
            asc: 'fa-solid fa-sort-up',
            desc: 'fa-solid fa-sort-down'
        },
        fallback: {
            neutral: 'fa-solid fa-sort',
            asc: 'fa-solid fa-sort-up',
            desc: 'fa-solid fa-sort-down'
        }
    };

    // Comparador ES con números naturales
    const collator = new Intl.Collator('es', { numeric: true, sensitivity: 'base' });

    function parseDate_ddMMyyyy(s) {
        if (!s) return 0;
        const m = String(s).trim().match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/);
        if (!m) return 0;
        const d = new Date(+m[3], (+m[2]) - 1, +m[1]);
        return d.getTime() || 0;
    }

    function parseNumber(s) {
        if (!s) return 0;
        // 1.234,56 -> 1234.56 ; Q 12.50 -> 12.50
        const clean = String(s)
            .replace(/[^\d\-.,]/g, '')
            .replace(/\.(?=\d{3}(?:\D|$))/g, '')
            .replace(',', '.');
        const n = parseFloat(clean);
        return isNaN(n) ? 0 : n;
    }

    function getCellValue(td) {
        const v = td.getAttribute('data-sort-value');
        return (v !== null && v !== undefined) ? v : td.textContent.trim();
    }

    function compareByType(a, b, type) {
        if (type.startsWith('date')) return parseDate_ddMMyyyy(a) - parseDate_ddMMyyyy(b);
        if (type === 'number') return parseNumber(a) - parseNumber(b);
        return collator.compare(a, b); // text (default)
    }

    // ===== Resolución de íconos por TH =====
    const ICONS_MAP = new WeakMap();

    function resolveIconsFor(th, type) {
        let base = ICONS_DEFAULTS.fallback;
        if (type) {
            if (type === 'number') base = ICONS_DEFAULTS.number;
            else if (type.startsWith('date')) base = ICONS_DEFAULTS.date;
            else if (type === 'text') base = ICONS_DEFAULTS.text;
        }
        // Overrides por columna (si existen)
        return {
            neutral: th.dataset.iconNeutral || base.neutral,
            asc: th.dataset.iconAsc || base.asc,
            desc: th.dataset.iconDesc || base.desc
        };
    }

    function clearSortState(ths, exceptIdx) {
        ths.forEach((th, i) => {
            if (i !== exceptIdx) {
                th.removeAttribute('aria-sort');
                const icon = th.querySelector('.ak-th-icon');
                const type = th.getAttribute('data-type') || '';
                const icons = ICONS_MAP.get(th) || resolveIconsFor(th, type);
                if (icon) icon.className = `${icons.neutral} ak-th-icon`; // neutral propio
            }
        });
    }

    function applySort(table, colIndex, type, dir) {
        const tbody = table.tBodies[0];
        const rows = Array.from(tbody.rows).map((tr, i) => ({ tr, i })); // estable

        rows.sort((r1, r2) => {
            const a = getCellValue(r1.tr.cells[colIndex]);
            const b = getCellValue(r2.tr.cells[colIndex]);
            const cmp = compareByType(a, b, type);
            if (cmp === 0) return r1.i - r2.i;
            return dir === 'asc' ? cmp : -cmp;
        });

        rows.forEach(r => tbody.appendChild(r.tr));
    }

    function initTable(table) {
        const ths = Array.from(table.tHead ? table.tHead.rows[0].cells : []);
        ths.forEach((th, idx) => {
            const type = th.getAttribute('data-type'); // solo ordenamos si hay data-type
            if (!type) return;

            // Resolve y memorice los íconos de este th
            const icons = resolveIconsFor(th, type);
            ICONS_MAP.set(th, icons);

            // Botón accesible + icono
            if (!th.querySelector('.ak-th-btn')) {
                const label = document.createElement('span');
                label.innerHTML = th.innerHTML;

                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'ak-th-btn';

                const icon = document.createElement('i');
                icon.className = `${icons.neutral} ak-th-icon`; // neutral inicial

                const ph = document.createElement('span'); // evita “salto” al cambiar ícono
                ph.className = 'ak-th-icon-placeholder';

                btn.appendChild(label);
                btn.appendChild(icon);
                th.innerHTML = '';
                th.appendChild(btn);
                th.appendChild(ph);
            }

            th.addEventListener('click', () => {
                // Alterna dirección
                const current = th.getAttribute('aria-sort');
                const dir = current === 'ascending' ? 'desc' : 'asc';

                clearSortState(ths, idx);
                th.setAttribute('aria-sort', dir === 'asc' ? 'ascending' : 'descending');

                const icon = th.querySelector('.ak-th-icon');
                const icons = ICONS_MAP.get(th) || resolveIconsFor(th, type);
                if (icon) icon.className = `${(dir === 'asc' ? icons.asc : icons.desc)} ak-th-icon`;

                applySort(table, idx, type, dir);
            });
        });
    }

    // Auto-init
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('table.js-sortable').forEach(initTable);
    });
})();
