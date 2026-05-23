# Ynclino Apartment Management System

An ASP.NET Core MVC application for managing apartment units, tenants, and day-to-day operations.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC 8.0
- **Database:** Microsoft SQL Server (LocalDB for dev, SQL Server Express / Standard for production)
- **ORM:** Entity Framework Core 8.0 (Code-First with Migrations)
- **UI:** Bootstrap 5.3 (via CDN)

---

## Features

### Master File
- **Units** — Full CRUD with status tracking (Vacant / Occupied / Under Maintenance)
- **Tenants** — Full CRUD with soft-delete (history preserved)

### Transactions
- Billing & Payment Monitoring *(scaffold)*
- Maintenance Request *(scaffold)*
- Lost & Found *(scaffold)*

### Built-in Business Rules
- Unit status auto-updates when a tenant is assigned or deactivated
- Tenant deactivation preserves billing & maintenance history
- Cannot delete a unit that still has active tenants
- Unique constraint on unit numbers and usernames

---

## Local Development Setup

### Requirements
- Visual Studio 2022 (or VS Code with C# Dev Kit)
- .NET 8 SDK
- SQL Server LocalDB *(included with Visual Studio)*

### Steps
1. Clone the repository
2. Open `YnclinoAMS.csproj` in Visual Studio
3. Press **F5**

That's it. On first run the app will automatically:
- Create the `YnclinoAMSDb` database in LocalDB
- Apply all EF Core migrations
- Open the browser at `https://localhost:7198`

---

## Production Deployment (On-Premise)

### Requirements on the target machine
- Windows Server / Windows 10 or 11
- .NET 8 Runtime (ASP.NET Core Hosting Bundle)
- SQL Server Express (free) or SQL Server Standard
- IIS *(optional but recommended)*

### Deployment Steps

1. **Install SQL Server Express** on the server machine

2. **Update the connection string** in `appsettings.json`:
   ```json
   "DefaultConnection": "Server=.\\SQLEXPRESS;Database=YnclinoAMSDb;Trusted_Connection=True;TrustServerCertificate=True"
   ```

3. **Publish the project** from Visual Studio:
   ```
   Build → Publish → Folder → publish/
   ```

4. **Deploy** by copying the `publish/` folder to the server. Run either:
   - As a Windows Service via `dotnet YnclinoAMS.dll`, or
   - Hosted in IIS (recommended for production)

5. On first launch, migrations apply automatically — no manual database setup required.

---

## Project Structure

```
YnclinoAMS/
├── Controllers/         MVC controllers (Units, Tenants, Billing, etc.)
├── Data/                ApplicationDbContext (EF Core)
├── Migrations/          Database schema history
├── Models/              Entity classes (tblUnit, tblTenant, tblUser)
│   └── ViewModels/      Form-binding view models
├── Properties/          launchSettings.json
├── Views/               Razor views (one folder per controller)
│   └── Shared/          Layout, partials, error page
├── wwwroot/             Static assets (CSS, JS)
├── appsettings.json     Connection string + logging config
├── Program.cs           App entry point
└── YnclinoAMS.csproj    Project file
```

---

## Backup & Maintenance

The database is a single SQL Server database called `YnclinoAMSDb`. Standard backup methods apply:

```sql
BACKUP DATABASE YnclinoAMSDb
TO DISK = 'C:\Backups\YnclinoAMSDb.bak'
WITH FORMAT, INIT, COMPRESSION;
```

Schedule via SQL Server Agent or Windows Task Scheduler for automated backups.

---

## License

Internal use only.
