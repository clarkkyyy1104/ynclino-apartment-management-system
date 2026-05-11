// Auto-dismiss alert messages after 4 seconds
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.alert.alert-success, .alert.alert-danger').forEach(function (el) {
        setTimeout(function () {
            var bsAlert = new bootstrap.Alert(el);
            bsAlert.close();
        }, 4000);
    });
});
