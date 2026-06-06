# SurveyMaker

A full-stack survey builder and response collection app built with ASP.NET Core 9 and Angular 20.

## Features

- Google OAuth sign-in — no passwords, accounts created automatically on first login
- Build forms with 24 question types (text, multiple choice, rating scales, NPS, Likert, calculations, and more)
- Section-based form layout with optional matrix questions and page-break control
- Public, private, and URL-only form visibility
- Response quotas, randomized question order
- Ocean-themed UI with a responsive layout

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 9, Kestrel |
| Frontend | Angular 20 (standalone components) |
| Auth | ASP.NET Identity + Google OAuth 2.0 |
| Database | SQL Server + Entity Framework Core 9 |
| ORM pattern | Code-first migrations, IEntityTypeConfiguration |

## Project Structure

```
src/
  SurveyMaker.Api/          # ASP.NET Core host + Angular SPA
    ClientApp/              # Angular 20 app
    Controllers/            # AccountController (auth endpoints)
  SurveyMaker.Core/         # Domain interfaces (stub, for future use)
  SurveyMaker.Infrastructure/
    Data/
      Entities/             # EF Core entity classes
      Configurations/       # IEntityTypeConfiguration classes
    Identity/               # ApplicationUser : IdentityUser
tests/
  SurveyMaker.UnitTests/
  SurveyMaker.IntegrationTests/
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- SQL Server (local or remote)
- A Google Cloud Console project with OAuth 2.0 credentials

### 1. Configure the database

Update the connection string in `src/SurveyMaker.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=SurveyMakerDb;Trusted_Connection=True;"
}
```

### 2. Set Google OAuth credentials

Never put these in config files. Store them in user secrets:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "<your-client-id>" --project src/SurveyMaker.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "<your-client-secret>" --project src/SurveyMaker.Api
```

In Google Cloud Console, add this as an authorized redirect URI:

```
http://localhost:44443/signin-google
```

### 3. Apply database migrations

```powershell
dotnet ef database update --project src/SurveyMaker.Infrastructure
```

This creates all tables and seeds the `QuestionTypes` (24 types) and `SecurityTypes` (Public, Private, Url Allowed) lookup tables.

### 4. Run the app

```powershell
dotnet run --project src/SurveyMaker.Api
```

The Angular dev server starts automatically on `http://localhost:44443`. The .NET backend listens on `https://localhost:7162` / `http://localhost:5065`.

## Deployment

See `production_deploy.md` for a step-by-step Azure App Service + Azure SQL deployment guide.
