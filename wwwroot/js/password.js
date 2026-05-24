// Password strength checker — shared by Create and Edit user forms

var RULES = {
    'r-len':     function (v) { return v.length >= 8; },
    'r-upper':   function (v) { return /[A-Z]/.test(v); },
    'r-lower':   function (v) { return /[a-z]/.test(v); },
    'r-digit':   function (v) { return /[0-9]/.test(v); },
    'r-special': function (v) { return /[^A-Za-z0-9]/.test(v); }
};

var SVG_CHECK = '<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="currentColor" viewBox="0 0 16 16"><path d="M13.854 3.646a.5.5 0 0 1 0 .708l-7 7a.5.5 0 0 1-.708 0l-3.5-3.5a.5.5 0 1 1 .708-.708L6.5 10.293l6.646-6.647a.5.5 0 0 1 .708 0z"/></svg>';
var SVG_DOT  = '<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="currentColor" viewBox="0 0 16 16"><circle cx="8" cy="8" r="3"/></svg>';

function checkStrength(value) {
    Object.keys(RULES).forEach(function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        var icon = el.querySelector('.rule-icon');
        var pass = RULES[id](value);
        el.classList.toggle('rule-pass', pass);
        el.classList.toggle('rule-fail', value.length > 0 && !pass);
        if (icon) icon.innerHTML = pass ? SVG_CHECK : SVG_DOT;
    });
}

function meetsAllRules(value) {
    return Object.values(RULES).every(function (fn) { return fn(value); });
}

function togglePw(inputId) {
    var input = document.getElementById(inputId);
    var eye   = document.getElementById(inputId + '-eye');
    var slash = document.getElementById(inputId + '-eye-slash');
    if (!input) return;
    if (input.type === 'password') {
        input.type = 'text';
        if (eye)   eye.style.display   = 'none';
        if (slash) slash.style.display = '';
    } else {
        input.type = 'password';
        if (eye)   eye.style.display   = '';
        if (slash) slash.style.display = 'none';
    }
}
