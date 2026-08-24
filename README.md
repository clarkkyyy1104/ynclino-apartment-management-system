# BKS Apartment Management System

An ASP.NET Core MVC application for managing tenants, billing, maintenance,
and lost & found for **BKS Apartment**.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC 8.0
- **Database:** MySQL 8.0 (or MariaDB 10.4+)
- **ORM:** Entity Framework Core 8.0 with the Pomelo MySQL provider
  (schema is created automatically on first run — no migrations to apply)
- **UI:** Bootstrap 5.3

---

## Features

- **Tenants** — full CRUD with soft-delete (history preserved), auto-generated
  login accounts, and emergency contact details
- **Billing & Payment** — issue and track bills; records amount paid, date paid,
  a **Payment Method** (Cash / GCash), and an auto-derived status
  (Paid / Partial / Unpaid / Late)
- **Maintenance** — requests with Minor/Moderate/Major/Urgent priority, issue
  types, photo attachments, and an Active/Archive view
- **Lost & Found** — report items with photos and handle claims
- **Roles** — Admin and Tenant, each with a tailored dashboard

> This build has **no Units module** — rent is entered directly on each bill, so
> the app suits a single property that is billed per tenant.

---

## Prerequisites

- Visual Studio 2022 (or VS Code with the C# Dev Kit)
- **.NET 8 SDK**
- **MySQL Server 8.0** and, optionally, **MySQL Workbench** for browsing the database

> ⚠️ MySQL **Workbench** is only a GUI — it is not the database. You must have
> **MySQL Server** installed and its Windows service (`MySQL80`) **running**.

---

## Database Setup

The app **creates the database and all tables automatically** on first run, so
you do not need to create anything by hand. You only need a running MySQL server
and your password in a local config file.

### 1. Make sure MySQL Server is running
`Win + R` → `services.msc` → find **MySQL80** → its status should be **Running**
(set *Startup type* to **Automatic** so it always starts). Note the **root
password** you set when installing MySQL.

### 2. Create `appsettings.Local.json` (your private config)
In the project root, copy `appsettings.Local.json.example` to
**`appsettings.Local.json`** and put your own MySQL password in it:

```json
{
  "MySqlPassword": "YOUR_PASSWORD_HERE"
}
```

- This file is **git-ignored** — your password is never committed or pushed.
- Only the **password** lives here. The rest of the connection string —
  including the **database name** (`BKSApartmentDb`) — comes from the committed
  `appsettings.json`.
- **Do not** put your real password in `appsettings.json`; leave its
  `YOUR_MYSQL_PASSWORD` placeholder untouched.

### 3. Run the app
Press **F5** in Visual Studio (or `dotnet run`). On first launch it:
- creates the `BKSApartmentDb` database and tables,
- seeds the default admin account,
- opens at **https://localhost:7251**.

### 4. Log in
- Default admin login: **`admin`** / **`Admin@123`**
- Change this password after the first sign-in (top-right menu → Change Password).

> **Resetting the database:** because the schema is created (not migrated), the
> quickest way to start fresh is to drop it and re-run the app:
> ```sql
> DROP DATABASE BKSApartmentDb;
> ```

---

## Project Structure

```
ynclino-apartment-management-system/
├── Controllers/         MVC controllers (Tenants, Billing, Maintenance, LostFound, ...)
├── Data/                ApplicationDbContext (EF Core)
├── Helpers/             Password hashing, image upload, notifications
├── Models/              Entity classes (tblTenant, tblBilling, ...)
│   └── ViewModels/      Form-binding view models
├── Views/               Razor views (one folder per controller)
│   └── Shared/          Layout, partials
├── wwwroot/
│   ├── css/             site.css, theme.css
│   ├── images/          Logos
│   └── uploads/         Runtime-uploaded photos (git-ignored)
├── appsettings.json              Config with a password placeholder (committed)
├── appsettings.Local.json        Your private DB password (git-ignored — you create this)
├── appsettings.Local.json.example  Template to copy
├── Program.cs                    App entry point
└── YnclinoApartmentManagementSystem.csproj
```

---

## Deployment

To host this online for the landlord, deploy the ASP.NET Core app to a host that
also provides a MySQL database (e.g. a free ASP.NET + MySQL host such as
MonsterASP.NET, or a container host). Publish a Release build, set the production
connection string via the host's environment/`appsettings` (never commit real
credentials), and the schema is created automatically on first run.

---

## Notes

- **Never commit your database password.** It belongs only in
  `appsettings.Local.json` (git-ignored).
- Uploaded photos are stored under `wwwroot/uploads/` and are git-ignored.

---

## License

Internal use only.
