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

    /* Auto-collapse. On a wide screen the sidebar sits as a rail and opens while
       the pointer is over it, folding back when the pointer leaves. data-peek
       lays the open sidebar over the page rather than pushing the page aside.
       Pinning it open with the menu button turns this off: data-rail is then
       absent, so there is nothing to fold back to. */
    var sidebar = document.getElementById('appSidebar');
    if (sidebar) {
        var wide = window.matchMedia('(min-width: 768px)');
        sidebar.addEventListener('mouseenter', function () {
            if (!wide.matches || !document.documentElement.hasAttribute('data-rail')) return;
            document.documentElement.removeAttribute('data-rail');
            document.documentElement.setAttribute('data-peek', '');
        });
        sidebar.addEventListener('mouseleave', function () {
            if (!document.documentElement.hasAttribute('data-peek')) return;
            document.documentElement.removeAttribute('data-peek');
            document.documentElement.setAttribute('data-rail', '');
        });
    }

    /* flash messages remove themselves; the close button removes one early */
    document.addEventListener('click', function (e) {
        var close = e.target.closest('[data-dismiss]');
        if (close && close.parentElement) close.parentElement.remove();
    });

        /* ── Modals ───────────────────────────────────────────────────────────
       The design has no separate Add/Update pages: every form is a panel
       floating over the list it belongs to, with the page behind it dimmed.

       Rather than rewrite fourteen views into fourteen dialogs, this takes the
       pages that already exist. A link marked data-modal-link is fetched, its
       [data-form-panel] is lifted out and dropped into the one dialog in the
       layout, and the form posts through fetch. A redirect back means the save
       worked; HTML back means validation failed, so we show the returned form
       with its messages, exactly as the full page would have.

       Nothing here is required for the app to work. Without <dialog> support,
       or with JavaScript off, the links stay ordinary links and every form is
       still reachable at its own address.                                    */
    var dlg = document.getElementById('appModal');
    var canModal = dlg && typeof dlg.showModal === 'function';

    /* Markup injected as HTML never runs its own <script> tags, so they are
       re-added here — one at a time, each waiting for the last. Appending them
       all at once looked fine and was not: jquery.validate would start loading
       beside jQuery instead of after it, and win the race often enough to throw
       "jQuery is not defined" on a slow load. */
    function runPageScripts(doc, done) {
        var holder = doc.querySelector('[data-page-scripts]');
        var list = holder ? Array.prototype.slice.call(holder.querySelectorAll('script')) : [];

        (function next(i) {
            if (i >= list.length) { done && done(); return; }
            var old = list[i];
            var src = old.getAttribute('src');

            /* a library the page already loaded does not need loading twice */
            if (src && document.querySelector('script[src="' + src + '"]')) { next(i + 1); return; }

            var s = document.createElement('script');
            if (src) {
                s.src = src;
                s.onload = s.onerror = function () { next(i + 1); };
                document.body.appendChild(s);
            } else {
                s.textContent = old.textContent;
                document.body.appendChild(s);
                next(i + 1);
            }
        })(0);
    }

    /* jQuery's unobtrusive validation wires itself up once, when the document is
       ready. A form that arrives afterwards is invisible to it, so every form we
       inject has to be handed over explicitly or the client-side messages never
       appear — the second and later modals especially, where the script is
       already loaded and nothing re-runs at all. */
    function wireValidation(scope) {
        var $ = window.jQuery;
        if (!$ || !$.validator || !$.validator.unobtrusive) return;
        var form = scope.querySelector('form');
        if (!form) return;
        $(form).removeData('validator').removeData('unobtrusiveValidation');
        $.validator.unobtrusive.parse(form);
    }

    function fillModal(html) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        /* a form page names its panel data-form-panel; a detail or confirm page
           names the part to lift data-modal-source */
        var panel = doc.querySelector('[data-form-panel], [data-modal-source]');
        if (!panel) return false;

        /* the kind decides the dialog's width and how its title reads */
        dlg.setAttribute('data-kind', panel.hasAttribute('data-confirm') ? 'confirm'
            : panel.matches('[data-form-panel]') ? 'form' : 'details');

        var head = panel.querySelector('[data-form-head], [data-panel-head]');
        dlg.querySelector('[data-modal-title]').textContent = head ? head.textContent.trim() : '';
        if (head) head.remove();          /* the dialog draws the heading itself */

        var body = dlg.querySelector('[data-modal-body]');
        body.innerHTML = '';
        body.appendChild(panel);
        runPageScripts(doc, function () { wireValidation(body); });
        return true;
    }

    function openModal(url) {
        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.text() : null; })
            .then(function (html) {
                /* anything unexpected: fall back to the page itself */
                if (html === null || !fillModal(html)) { window.location = url; return; }
                if (!dlg.open) dlg.showModal();
            })
            .catch(function () { window.location = url; });
    }

    if (canModal) {
        document.addEventListener('click', function (e) {
            var link = e.target.closest('[data-modal-link]');
            if (link && link.getAttribute('href')) {
                e.preventDefault();
                openModal(link.getAttribute('href'));
                return;
            }
            if (e.target.closest('[data-modal-close]') && dlg.open) { e.preventDefault(); dlg.close(); return; }
            /* Cancel and Back inside a modal lead to the list it is already floating
               over, so they close it rather than reload that same page */
            var back = e.target.closest('[data-modal-body] [data-actions-row] a[href]:not([data-modal-link])');
            /* a list page is one path segment deep: /Units, /Tenants */
            if (back && dlg.open && new URL(back.href, location.href).pathname.split('/').filter(Boolean).length === 1) {
                e.preventDefault(); dlg.close(); return;
            }
            /* the backdrop is the dialog itself; the panel sits inside it */
            if (e.target === dlg) dlg.close();
        });

        dlg.addEventListener('submit', function (e) {
            var form = e.target;
            if (form.tagName !== 'FORM') return;
            /* onsubmit="return confirm(...)", a password-rules check or client
               validation already said no — posting anyway would ignore the answer */
            if (e.defaultPrevented) return;
            e.preventDefault();

            var submit = form.querySelector('[type="submit"]');
            if (submit) submit.disabled = true;

            fetch(form.action, {
                method: (form.method || 'post'),
                /* the clicked button carries its own name and value (Approve / Reject),
                   which FormData leaves out unless it is told which button it was */
                body: (function () { try { return new FormData(form, e.submitter); } catch (err) { return new FormData(form); } })(),
                /* Manual, so the browser does NOT quietly fetch the redirect
                   target for us. Letting it follow cost us every success
                   message: the controller puts one in TempData, fetch's own
                   silent GET read it, and TempData is read-once — so by the
                   time the real navigation happened the message was gone. We
                   only need to know the save worked; reloading shows it. */
                redirect: 'manual',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(function (r) {
                    /* saved: the controller answered with a redirect */
                    if (r.type === 'opaqueredirect' || r.status === 0) { window.location.reload(); return null; }
                    return r.text();
                })
                .then(function (html) {
                    if (html === null) return;
                    if (submit) submit.disabled = false;
                    /* rejected: show the form again, with its messages */
                    if (!fillModal(html)) window.location.reload();
                })
                .catch(function () { if (submit) submit.disabled = false; form.submit(); });
        });

        /* closing leaves nothing behind for the next form to trip over */
        dlg.addEventListener('close', function () {
            dlg.querySelector('[data-modal-body]').innerHTML = '';
            dlg.querySelector('[data-modal-title]').textContent = '';
        });
    }

    window.addEventListener('DOMContentLoaded', function () {
        /* the head script set the attribute before paint; the button has to agree */
        if (document.documentElement.hasAttribute('data-rail')) setRail(true);
        document.querySelectorAll('[data-flash]').forEach(function (msg) {
            setTimeout(function () { msg.remove(); }, 5000);
        });
    });
})();
