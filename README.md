# Ynclino Apartment Management System

An ASP.NET Core MVC application for managing apartment units, tenants, billing,
maintenance, and lost & found.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC 8.0
- **Database:** MySQL 8.0 (or MariaDB 10.4+)
- **ORM:** Entity Framework Core 8.0 with the Pomelo MySQL provider
  (schema is created automatically on first run — no migrations to apply)
- **UI:** Bootstrap 5.3 with a custom Ynclino theme

---

## Features

- **Units** — full CRUD; separate Deposit and One-Month-Advance (each auto-fills to one month's rent); status tracking (Vacant / Occupied / Under Maintenance)
- **Tenants** — full CRUD with soft-delete (history preserved), emergency contact details
- **Billing & Payment** — issue and track bills
- **Maintenance** — requests with Low/Medium/High/Urgent priority, issue types, photo attachments, and an Active/Archive view
- **Lost & Found** — report items with photos and handle claims
- **Roles** — Admin and Tenant, each with a tailored dashboard

---

## Prerequisites

- Visual Studio 2022 (or VS Code with the C# Dev Kit)
- **.NET 8 SDK**
- **MySQL Server 8.0** (installed via the *MySQL Installer for Windows*) and,
  optionally, **MySQL Workbench** for browsing the database

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
- Only the **password** lives here now. The rest of the connection string —
  including the **database name** — comes from the committed `appsettings.json`,
  so each branch can point at its own database and this one local file works on
  every branch (no editing when you switch branches).
- **Do not** put your real password in `appsettings.json`; leave its
  `YOUR_MYSQL_PASSWORD` placeholder untouched.
- Each teammate creates their own `appsettings.Local.json` with their own password.

> Different databases per branch: the `crud` branch uses `YnclinoAMS_crud` and
> `crud(copy)` uses `YnclinoApartmentManagementSystemDb`, so the two branches never
> share data. (Set in each branch's `appsettings.json`.)

### 3. Run the app
Press **F5** in Visual Studio (or `dotnet run`). On first launch it:
- creates the `YnclinoApartmentManagementSystemDb` database and tables,
- seeds the default admin account,
- opens at **https://localhost:7251**.

### 4. Log in and load demo data
- Default admin login: **`admin`** / **`Admin@123`**
- On the **Units** page, click **Load Sample Data** to populate demo units and tenants.

> **Resetting the database:** because the schema is created (not migrated), the
> quickest way to start fresh is to drop it and re-run the app:
> ```sql
> DROP DATABASE YnclinoApartmentManagementSystemDb;
> ```

### Forgot your MySQL root password?
Stop the `MySQL80` service, create `C:\mysql-init.txt` containing
`ALTER USER 'root'@'localhost' IDENTIFIED BY 'NewPass@2026';`, then from an
**Administrator** Command Prompt run:
```
"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld" --defaults-file="C:\ProgramData\MySQL\MySQL Server 8.0\my.ini" --init-file="C:\mysql-init.txt" --console
```
Press `Ctrl+C` after it starts, restart the service, delete the init file, and
update `appsettings.Local.json` with the new password.

---

## Brand & Theme

The Ynclino visual identity is derived from the logo.

### Colors

| Role | Hex | Used for |
|------|-----|----------|
| **Brand orange** | `#ff8235` | Buttons, active nav item, table/card top borders, profile pill, login accents |
| **Orange (hover/active)** | `#e5701f` | Button hover/active states |
| **Charcoal** | `#37383a` | Sidebar, table headers, panel/card headers, stat-card caps |
| **Cream / off-white** | `#ecebe4` | Text on dark (login, sidebar wordmark) |
| Page background | `#f6f8fb` | App content area |

There is **no blue** in the palette. Brand tokens live as CSS variables in
`wwwroot/css/theme.css` (`--ynk-orange`, `--ynk-dark`, etc.); Bootstrap's
`primary` is mapped to charcoal so all default "primary" fills stay on-brand.

### Logos

Source SVGs are in `Logos/`; web-ready copies used by the app are in
`wwwroot/images/`:

| File | Variant | Where it's used |
|------|---------|-----------------|
| `ynclino-logo-text-light.svg` | Wordmark, light | Sidebar and login (dark backgrounds) |
| `ynclino-logo-light.svg` | Icon only, light | For dark backgrounds |
| `ynclino-logo-dark.svg` | Icon only, dark | Browser favicon (light backgrounds) |

> A dark-background **wordmark** (icon + text) is not yet available; when it is,
> drop it into `wwwroot/images/` and it can be used on any light surface.

---

## Project Structure

```
ynclino-apartment-management-system/
├── Controllers/         MVC controllers (Units, Tenants, Billing, Maintenance, LostFound, ...)
├── Data/                ApplicationDbContext (EF Core)
├── Helpers/             Password hashing, image upload helper
├── Logos/               Source logo SVGs
├── Models/              Entity classes (tblUnit, tblTenant, ...)
│   └── ViewModels/      Form-binding view models
├── Views/               Razor views (one folder per controller)
│   └── Shared/          Layout, partials
├── wwwroot/
│   ├── css/             site.css, theme.css (brand theme)
│   ├── images/          Logos
│   └── uploads/         Runtime-uploaded photos (git-ignored)
├── appsettings.json              Config with a password placeholder (committed)
├── appsettings.Local.json        Your private DB password (git-ignored — you create this)
├── appsettings.Local.json.example  Template to copy
├── Program.cs                    App entry point
└── YnclinoApartmentManagementSystem.csproj
```

---

## Notes for the team

- **Never commit your database password.** It belongs only in
  `appsettings.Local.json` (git-ignored). If you accidentally commit it, change
  your MySQL password and remove it from the tracked file.
- Uploaded photos are stored under `wwwroot/uploads/` and are git-ignored, so
  they stay on each person's machine.

---

## License

Internal use only.
