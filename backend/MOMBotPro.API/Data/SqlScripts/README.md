# MOMBotPro — SQL Scripts

## Setup Instructions

> **Note:** Tables are created automatically via EF Core Migrations.
> These scripts are ONLY for Stored Procedures.

### Steps to Set Up a New Local Environment

1. **Run EF Migrations first** (creates all tables):
   ```bash
   cd backend/MOMBotPro.API
   dotnet ef database update
   ```

2. **Then run these SQL scripts in SSMS** (in this order):
   ```
   StoredProcedures/sp_GetUserByEmail.sql
   StoredProcedures/sp_GetUserById.sql
   StoredProcedures/sp_CreateUser.sql
   StoredProcedures/sp_DecrementFreeTrial.sql
   StoredProcedures/sp_UpdateSubscription.sql
   StoredProcedures/sp_GetIntegrationsByUser.sql
   StoredProcedures/sp_SaveIntegration.sql
   ```

   Or run them all at once using:
   ```
   StoredProcedures/00_RunAll.sql
   ```

### Database: `MOMBotProDB`

All stored procedures target the `MOMBotProDB` database.
Make sure your `appsettings.json` `DefaultConnection` points to the correct server.
