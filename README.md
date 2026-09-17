# Ynclino Apartment Management System

An ASP.NET Core MVC application for managing apartment units, tenants, billing,
maintenance, and lost & found.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC 8.0
- **Database:** MySQL 8.0 (or MariaDB 10.4+)
- **ORM:** Entity Framework Core 8.0 with the Pomelo MySQL provider
  (the database is a real one, built by running `Database/ynclino_schema.sql`;
  the program connects to it and never creates it)
- **UI:** hand-written CSS in `wwwroot/css/ynclino.css` — no framework

---

## Features

- **Units** — full CRUD; separate Deposit and One-Month-Advance (each auto-fills to one month's rent); status tracking (Available / Occupied / Under Maintenance)
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

The database is a **real MySQL database that you build once from a script**. The
program does not create it. This changed on purpose: the app used to call EF
Core's `EnsureCreated()` at startup, which quietly built whatever the models
happened to say at that moment — so the schema in the database and the schema in
the repository could drift apart with nobody noticing. Now there is one written
source of truth, `Database/ynclino_schema.sql`, and it is the thing that gets
run. If the database is missing or its tables do not match, startup stops with
an error instead of serving broken pages.

### 1. Make sure MySQL Server is running
`Win + R` → `services.msc` → find **MySQL80** → its status should be **Running**
(set *Startup type* to **Automatic** so it always starts). Note the **root
password** you set when installing MySQL.

### 2. Build the database from the script
From the project root, run the script once:

```
mysql -u root -p < Database/ynclino_schema.sql
```

It creates `YAMSDB`, its 11 tables with their keys and relationships, and
the one administrator account you need to sign in the first time
(**`admin`** / **`Admin@123`**).

> In MySQL Workbench you can do the same with **File → Open SQL Script…**, pick
> `Database/ynclino_schema.sql`, then hit the lightning bolt to execute it.

If you created `YAMSDB` from an earlier version of the SQL file, run
`Database/yamsdb_app_compat.sql` once in Workbench instead of rebuilding it.
That patch adds only the columns required by the current screens.
For a `YAMSDB` created before Lost & Found archiving was added, run
`Database/yamsdb_lostfound_archive.sql` once in Workbench. It adds the archive
date without changing existing item or claim records.

### 3. Create `appsettings.Local.json` (your private config)
In the project root, create **`appsettings.Local.json`** with your MySQL
password:

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

The committed `appsettings.json` points to `YAMSDB`. A full
`ConnectionStrings:DefaultConnection` in `appsettings.Local.json` overrides it;
make sure that override also names `YAMSDB`. If MySqlConnector reports a local
Windows SSL authentication error, add `SslMode=Disabled` to a **localhost-only**
connection string in the private file.

### 4. Run the app
Press **F5** in Visual Studio (or `dotnet run`). It connects to the database you
built in step 2 and opens at **https://localhost:7251**.

If startup reports that it cannot connect, check the MySQL service and both
connection-string files. If it reports a model mismatch, apply the compatibility
patch described in step 2.

### 5. Log in and load demo data
- Default admin login: **`admin`** / **`Admin@123`**
- On the **Units** page, click **Load Sample Data** to populate demo units and tenants.

> **Resetting the database deletes all its records.** Run the script again only
> if a fresh database is intended. The script itself drops `YAMSDB` first.
> ```sql
> DROP DATABASE YAMSDB;
> ```
> ```
> mysql -u root -p < Database/ynclino_schema.sql
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

There is **no blue** in the palette. Brand tokens live as CSS custom properties
at the top of `wwwroot/css/ynclino.css`. There is no CSS framework — the
stylesheet is hand-written, and components are named with `data-*` attributes
(`[data-panel]`, `[data-stat]`, `[data-table-wrap]`) rather than inferred from
document structure.

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
├── Database/            ynclino_schema.sql — the script that builds the database
├── Helpers/             Password hashing, image upload helper
├── Services/            SystemNotificationService — derives alerts from the records
├── Logos/               Source logo SVGs
├── Models/              Entity classes (tblUnit, tblTenant, ...)
│   └── ViewModels/      Form-binding view models
├── Views/               Razor views (one folder per controller)
│   └── Shared/          Layout, partials
├── wwwroot/
│   ├── css/             ynclino.css (the stylesheet), site.css, theme.css
│   ├── js/              ynclino.js (nav rail, collapsible groups)
│   ├── images/          Logos
│   └── uploads/         Runtime-uploaded photos (git-ignored)
├── appsettings.json              Config with a password placeholder (committed)
├── appsettings.Local.json        Your private DB password (git-ignored — you create this)
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
