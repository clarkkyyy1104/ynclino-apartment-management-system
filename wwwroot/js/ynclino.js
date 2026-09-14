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
    /* The sidebar rail. One place sets it, so the button's own wording can never
       drift out of step with the sidebar it describes — widening it by clicking
       a group has to relabel the button too. */
    function setRail(collapsed) {
        document.documentElement.toggleAttribute('data-rail', collapsed);
        try { localStorage.setItem('ynclino-rail', collapsed ? '1' : '0'); } catch (err) { }

        var toggle = document.querySelector('[data-rail-toggle]');
        if (!toggle || !window.matchMedia('(min-width: 768px)').matches) return;
        var word = collapsed ? 'Expand the menu' : 'Collapse the menu';
        toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        toggle.setAttribute('title', word);
        toggle.setAttribute('aria-label', word);
    }

    /* The one button above the module. What it means depends on the width,
       because the sidebar itself does: wide, it is a column that narrows to a
       rail; narrow, it is a drawer that is either in or out. There is no rail
       below 768px, so pressing it there must slide the drawer instead. */
    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('[data-rail-toggle]');
        if (!toggle) return;
        e.preventDefault();

        if (window.matchMedia('(min-width: 768px)').matches) {
            setRail(!document.documentElement.hasAttribute('data-rail'));
        } else {
            var side = document.getElementById('appSidebar');
            if (side) side.toggleAttribute('data-open');
        }
    });

    /* disclosure groups in the sidebar: <a data-collapse="#unitsGroup"> */
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-collapse]');
        if (!trigger) return;
        e.preventDefault();

        var panel = document.querySelector(trigger.getAttribute('data-collapse'));
        if (!panel) return;


        /* there is nowhere to show a sub-list on a collapsed rail, so the first
           click widens the sidebar and leaves the group for the next one */
        if (document.documentElement.hasAttribute('data-rail')) {
            setRail(false);
            return;
        }

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
        /* the head script set the attribute before paint; the button has to agree */
        if (document.documentElement.hasAttribute('data-rail')) setRail(true);
        document.querySelectorAll('[data-flash]').forEach(function (msg) {
            setTimeout(function () { msg.remove(); }, 5000);
        });
    });
})();
