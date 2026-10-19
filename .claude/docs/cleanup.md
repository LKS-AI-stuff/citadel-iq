# Cleaning up CitadelIQ data

Steps to wipe CitadelIQ data locally, in Docker and in Azure. Every step is destructive and can't be undone.

- **Level 1** resets all data but keeps the setup working.
- **Level 2** also tears down the Azure resources and local secrets.

Before you start, stop the API (`Ctrl+C` on `dotnet run`) and the UI dev server.

## Level 1: Reset all data, keep the setup

### 1. Local database

Choose one option.

**Option a: delete only the rows.** This keeps the container, the schema and the `citadeliq_app` login.

```bash
docker exec -it citadeliq-pg psql -U postgres -d citadeliq -c \
  'TRUNCATE "DocumentChunks", "Documents", "Folders", "JoinRequests", "Memberships", "Users", "Workspaces";'
```

**Option b: delete the whole database.** This also removes the `citadeliq_app` login.

```bash
docker rm -f citadeliq-pg
docker volume rm citadeliq-pgdata
```

To start again, re-run the `docker run …` command from the [README](../../README.md#1-database-setup-postgresql--pgvector),
then create `citadeliq_app` again with `CREATE ROLE citadeliq_app …`.

### 2. Local uploaded files

```bash
rm -rf CitadelIQ.Api/App_Data/documents/*
```

### 3. Azure Blob files (only if you used `Storage:Provider=AzureBlob`)

```bash
az login
az storage blob delete-batch --account-name <account> --source documents --auth-mode login
az storage blob list --account-name <account> --container-name documents --auth-mode login -o table   # should be empty
```

If soft delete is on for the container, deleted blobs stay recoverable until the retention period ends. To purge them
sooner, delete the container (step 10).

### 4. Entra External ID users

In [entra.microsoft.com](https://entra.microsoft.com), switch to the **CitadelIQ** tenant:

1. Go to **Users → All users**. Select the users who signed up through the app and click **Delete**. **Don't delete your
   own admin account.**
2. Go to **Users → Deleted users**. Select the same users and click **Delete permanently**. Otherwise they stay
   recoverable for 30 days, and their emails can't sign up again until then.

This step is needed because an account deleted only from the database still exists in Entra. Signing in again would
just create a new CitadelIQ user that starts at onboarding.

### 5. Browser session

Use **Sign out** in the app. Alternatively, clear the site data for `http://localhost:5173`: DevTools → Application →
Storage → **Clear site data**. A leftover session cookie would otherwise recreate your user on the next request.

### 6. Docker Compose stack (only if you've run it)

```bash
docker compose down -v      # removes the pgdata, documents and dpkeys volumes
```

### 7. Leftover test containers (from integration-test runs)

```bash
docker ps -a --filter "label=org.testcontainers=true"
docker rm -f $(docker ps -aq --filter "label=org.testcontainers=true")
```

The second command errors harmlessly if the first one listed nothing.

## Level 2: Full teardown (do Level 1 first)

### 8. Entra app registration and user flow

- **App registrations → CitadelIQ (local)** (and the production registration, if you created one) **→ Delete**. This
  also invalidates its client secrets.
- Then **App registrations → Deleted applications → Delete permanently**.
- **External Identities → User flows → SignUpSignIn → Delete**.

### 9. The Entra tenant itself

1. Switch to the CitadelIQ tenant. Go to **Overview → Manage tenants**, select it and click **Delete**.
2. The page lists any blockers: remaining users, apps, user flows or a linked subscription. Clear each one and retry.
3. In the Azure portal, check `rg-citadeliq` (or whichever resource group you chose) for the tenant's resource, of type
   *Microsoft.AzureActiveDirectory/ciamDirectories*. Delete it if it's still there.

Deleting the tenant is permanent, and the `citadeliq` domain name may not be reusable immediately.

### 10. Azure Storage

- **Only the container:**

  ```bash
  az storage container delete --account-name <account> --name documents --auth-mode login
  ```

- **Everything in the resource group:**

  ```bash
  az group delete --name rg-citadeliq
  ```

  This deletes the storage account, **and the tenant's resource too if it lives in that group**. Delete the tenant
  first (step 9) so you don't orphan it.
- If you keep the storage account, remove the *Storage Blob Data Contributor* role assignment you gave yourself.

### 11. Local secrets and keys

```bash
cd CitadelIQ.Api
dotnet user-secrets list     # review first
dotnet user-secrets clear    # removes ALL of this project's secrets (OpenAI key, connection strings, OIDC, storage)
rm -rf ~/.aspnet/DataProtection-Keys   # session-cookie keys; also signs out other local ASP.NET apps
```

To remove only some entries, use `dotnet user-secrets remove "<key>"` instead of `clear`, e.g.
`dotnet user-secrets remove "Authentication:Oidc:ClientSecret"`.

### 12. OpenAI key (optional)

The app stores nothing at OpenAI. If you're retiring the project, revoke the key at
[platform.openai.com](https://platform.openai.com) → API keys.

## Check that everything is clean

- `docker ps -a` and `docker volume ls` show no `citadeliq` containers or volumes, and no Testcontainers leftovers.
- `CitadelIQ.Api/App_Data/documents/` is empty.
- In Entra, **Users** lists only your admin account (Level 1), or the tenant is gone (Level 2).
- The blob container is empty or deleted.
