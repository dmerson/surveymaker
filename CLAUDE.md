# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Running the application
```powershell
# From repo root — starts both the .NET backend and Angular dev server
dotnet run --project src/SurveyMaker.Api
```
The SpaProxy hosting startup launches `npm start` automatically (Angular on `http://localhost:44443`). The .NET backend listens on `https://localhost:7162` and `http://localhost:5065`.

### Running tests
```powershell
# All tests
dotnet test

# Single test project
dotnet test tests/SurveyMaker.UnitTests
dotnet test tests/SurveyMaker.IntegrationTests

# Single test by name
dotnet test --filter "FullyQualifiedName~MyTestMethod"
```

Angular unit tests (Karma/Jasmine):
```powershell
cd src/SurveyMaker.Api/ClientApp
npm test
```

### Building
```powershell
dotnet build
```
The API `.csproj` runs `npm install` and `ng build` automatically on publish/production builds via MSBuild targets.

### EF Core migrations
Run migrations from the **repo root** targeting the Infrastructure project — no `--startup-project` needed because `SurveyMakerDbContextFactory` resolves `appsettings.json` at design time:
```powershell
dotnet ef migrations add <MigrationName> --project src/SurveyMaker.Infrastructure
dotnet ef database update --project src/SurveyMaker.Infrastructure
```

### Google OAuth credentials (required for sign-in)
Credentials must live in User Secrets, never in any config file:
```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/SurveyMaker.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>" --project src/SurveyMaker.Api
```

## Architecture

### Project layout
```
src/
  SurveyMaker.Api/          # ASP.NET Core 9 host + Angular SPA
    ClientApp/              # Angular 20 app (standalone components)
    Controllers/            # AccountController (auth endpoints)
    Properties/launchSettings.json
  SurveyMaker.Core/         # Domain models and interfaces (currently a stub)
  SurveyMaker.Infrastructure/
    Data/                   # SurveyMakerDbContext (IdentityDbContext<ApplicationUser>)
    Identity/               # ApplicationUser : IdentityUser
    ServiceRegistration.cs  # AddInfrastructure() extension method
tests/
  SurveyMaker.UnitTests/    # xUnit + Moq + FluentAssertions + EF InMemory
  SurveyMaker.IntegrationTests/  # WebApplicationFactory-based
```

### Dev server proxy and why HTTP is used
In development, the Angular dev server (`http://localhost:44443`) proxies `/api` and `/signin-google` to the **.NET HTTP port** (`http://localhost:5065`) — not HTTPS. This is intentional:

- The proxy preserves the original `Host: localhost:44443` header, so ASP.NET Core generates OAuth redirect URIs using the Angular dev server host, not the backend host. The registered Google Cloud Console redirect URI must be `http://localhost:44443/signin-google`.
- The auth cookie has no `Secure` flag in development (`CookieSecurePolicy.None`) so it round-trips over HTTP through the proxy.
- `UseHttpsRedirection` is skipped in development to avoid redirect loops.
- The Google OAuth correlation cookie is configured with `SameSite=Lax` and `SecurePolicy.None` in development because `SameSite=None` without `Secure` is rejected by modern browsers. `Lax` is safe here since the OAuth callback is a top-level GET navigation.

### Authentication flow
1. Angular calls `GET /api/account/login` (proxied to backend)
2. Backend issues a Google OAuth challenge; the callback URL is `http://localhost:44443/signin-google` (using the preserved Host header)
3. Google redirects browser to `http://localhost:44443/signin-google?code=...`
4. The Angular dev proxy forwards this to `http://localhost:5065/signin-google` — the Google OAuth **middleware** processes the code exchange
5. Middleware redirects to `/api/account/google-callback` — the **controller action** calls `GetExternalLoginInfoAsync()`, creates the user if new, calls `SignInAsync`, sets the auth cookie
6. Controller redirects to `/`, SpaProxy redirects to Angular dev server, Angular calls `/api/account/user` to hydrate user state

### Infrastructure and ASP.NET Core APIs from class libraries
`SurveyMaker.Infrastructure` uses `Microsoft.NET.Sdk` (not the Web SDK). To access `AddIdentity`, `IServiceCollection`, etc., it has:
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```
Without this, Identity extension methods are unavailable.

### Angular app
Angular 20 uses standalone components throughout — no NgModules. Use `inject()` instead of constructor injection. Control flow uses `@if`/`@for` syntax (not `*ngIf`/`*ngFor`). Components are in `src/app/`, services in `src/app/services/`, models in `src/app/models/`.

### Database
SQL Server at `MSI\DONEMERSON` with Windows Authentication. `SurveyMakerDbContext` extends `IdentityDbContext<ApplicationUser>`. `SurveyMakerDbContextFactory` walks up the directory tree to find `appsettings.json` so migrations work without the app running.
