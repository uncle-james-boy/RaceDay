# RaceDay local Docker deployment

This setup runs the MVC site, API, SQL Server and Azurite locally in Docker Desktop. MVC talks only to the API. The API talks to SQL Server and Azurite. It does not create or connect to an Azure account.

## Prerequisites

- Docker Desktop running with **Linux containers** selected.
- SQL Server Management Studio (SSMS) for the one-time database creation.
- Azure CLI for the repeatable Azurite container setup script.

## First-time setup

1. From the repository root, make a local environment file and set a strong, unique password for this local SQL Server container:

   ```powershell
   Copy-Item .env.example .env
   notepad .env
   ```

   `.env` is ignored by Git. Do not add credentials to the repository. The storage connection string in the example targets only Azurite's public development account; it is not an Azure credential.

2. Start only the database and storage emulator:

   ```powershell
   docker compose up -d sql azurite
   docker compose ps
   ```

3. Initialize the **fresh Docker SQL volume once** in SSMS:

   - Connect to server `localhost,14330` using SQL Server Authentication and the local `sa` password from `.env`.
   - Open `docs/RaceDay_Database.sql` and execute it against that Docker SQL Server.
   - Confirm the script reports completion and that `RaceDayDB` contains the six RaceDay tables.

   The script creates `RaceDayDB` and refuses to replace existing RaceDay tables. Do not rerun it against a database that already has the tables. This Docker database is separate from the Windows `.SQLEXPRESS` database.

4. Install Azure CLI if needed, then create/configure the two Azurite containers. This command can be repeated; it does not delete blobs:

   ```powershell
   .\tools\setup-azurite.ps1
   ```

   It sets `event-banners` to public Blob-read access and `profile-pictures` to private access.

## Build and start the applications

```powershell
docker compose config
docker compose build api web
docker compose up -d api web
docker compose ps
```

Open:

- MVC site: <http://localhost:5157>
- API Swagger: <http://localhost:7107/swagger>
- API readiness: <http://localhost:7107/health/ready>

The API uses Docker's internal SQL address `sql` and Azurite address `azurite`. The MVC client uses `http://api:8080/`. The API has one replica because its session data is held in process memory. Local HTTP is used intentionally; the API HTTPS redirect is disabled only in the Compose environment.

Banner URLs are saved with the browser-visible `http://localhost:10000/devstoreaccount1` base. The API itself connects to Azurite using the Docker-internal endpoint. Profile pictures remain private and are returned only by the authenticated API flow.

## Stop and preserve local data

Stop containers while keeping all three named volumes:

```powershell
docker compose down
```

Starting again with `docker compose up -d` reuses the SQL database, Blob data and Linux Data Protection keys. **Do not use `docker compose down -v`** unless you intentionally want Docker to remove the persistent volumes and their data. The repository's existing `.data-protection-keys/` directory is not mounted, modified or included in the images.

## Troubleshooting

- If the API is unhealthy, confirm the Docker SQL service is healthy and that the one-time SQL setup completed in its volume.
- If uploads fail, run `tools/setup-azurite.ps1` and check that the Azurite service is running.
- If a banner URL does not load, confirm the browser can open `http://localhost:10000` and check the saved URL begins with `http://localhost:10000/devstoreaccount1/event-banners/`.
- Restarting the API clears its in-memory API sessions; sign in again after a restart.
- The first image build needs network access to restore NuGet packages and pull Microsoft container images.
