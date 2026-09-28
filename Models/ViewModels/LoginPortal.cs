namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    // One of the three sign-in pages. They share a single form and a single view;
    // what differs is who each page is for. Each accepts only its own role, so a
    // staff account typed into the tenant page is turned away instead of being
    // let in.
    public sealed class LoginPortal
    {
        // the controller action that serves this page
        public string Action { get; }

        // the only role allowed to sign in here
        public string Role { get; }

        // how the page names its audience, e.g. "maintenance staff"
        public string Audience { get; }

        public string Heading => $"Please sign in to your {Audience} account";
        public string Title { get; }

        // Whether another page may show a link to this one. Only the tenant page
        // is. The staff pages' addresses are given to staff in person and are
        // never shown on screen, so nobody finds them by clicking around.
        public bool IsListed { get; }

        private LoginPortal(string action, string role, string audience, string title, bool isListed)
        {
            Action = action;
            Role = role;
            Audience = audience;
            Title = title;
            IsListed = isListed;
        }

        // Tenant is first on purpose: it is the default sign-in, served at
        // /Account/Login, which is where the cookie sends anyone not signed in.
        public static readonly LoginPortal Tenant =
            new("Login", "Tenant", "tenant", "Tenant Sign In", isListed: true);

        public static readonly LoginPortal Maintenance =
            new("MaintenanceLogin", "Maintenance", "maintenance staff", "Maintenance Sign In", isListed: false);

        public static readonly LoginPortal Admin =
            new("AdminLogin", "Admin", "administrator", "Administrator Sign In", isListed: false);

        public static readonly IReadOnlyList<LoginPortal> All = new[] { Tenant, Maintenance, Admin };

        // the page a given role belongs on; anything unrecognised goes to the default
        public static LoginPortal For(string? role) =>
            All.FirstOrDefault(p => p.Role == role) ?? Tenant;
    }
}
