/* ==========================================================================
   YNCLINO — behaviour only. No styling, no framework.

   State is expressed with real HTML attributes, not CSS classes, so the
   markup stays free of design decisions:
     hidden          – the browser hides it with no stylesheet at all
     aria-expanded   – whether a disclosure is open
     data-open       – the mobile sidebar
   Style them with attribute selectors, e.g.  a[aria-current="page"] { ... }
   ========================================================================== */
(function () {
    'use strict';

    /* disclosure groups in the sidebar: <a data-collapse="#unitsGroup"> */
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-collapse]');
        if (!trigger) return;
        e.preventDefault();

        var panel = document.querySelector(trigger.getAttribute('data-collapse'));
        if (!panel) return;

        var nowOpen = panel.hasAttribute('hidden');
        panel.toggleAttribute('hidden');
        trigger.setAttribute('aria-expanded', nowOpen ? 'true' : 'false');
    });

    /* the account menu: <a data-dropdown aria-controls="accountMenu"> */
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-dropdown]');

        // any click elsewhere closes whatever is open
        document.querySelectorAll('[data-dropdown][aria-expanded="true"]').forEach(function (t) {
            if (t === trigger) return;
            t.setAttribute('aria-expanded', 'false');
            var m = document.getElementById(t.getAttribute('aria-controls'));
            if (m) m.setAttribute('hidden', '');
        });

        if (!trigger) return;
        e.preventDefault();

        var menu = document.getElementById(trigger.getAttribute('aria-controls'));
        if (!menu) return;

        var nowOpen = menu.hasAttribute('hidden');
        menu.toggleAttribute('hidden');
        trigger.setAttribute('aria-expanded', nowOpen ? 'true' : 'false');
    });

    /* Escape closes an open menu, which keyboard users expect */
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        document.querySelectorAll('[data-dropdown][aria-expanded="true"]').forEach(function (t) {
            t.setAttribute('aria-expanded', 'false');
            var m = document.getElementById(t.getAttribute('aria-controls'));
            if (m) m.setAttribute('hidden', '');
        });
    });

    /* flash messages remove themselves; the close button removes one early */
    document.addEventListener('click', function (e) {
        var close = e.target.closest('[data-dismiss]');
        if (close && close.parentElement) close.parentElement.remove();
    });

    window.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-flash]').forEach(function (msg) {
            setTimeout(function () { msg.remove(); }, 5000);
        });
    });
})();
