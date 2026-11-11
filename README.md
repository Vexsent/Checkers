# Checkers
A simple checkers application

## Deploying the server with Docker

The server (API + live-match hub) runs in a container behind a reverse proxy that handles HTTPS.
It uses an existing SQL Server; the database and its tables must already exist.

On the home server (Linux x64 with Docker and the compose plugin):

```sh
git clone https://github.com/Vexsent/Checkers.git
cd Checkers
cp .env.example .env    # then fill in the connection string and a signing key
docker compose up -d --build
curl http://localhost:8080/health    # should print "Healthy"
```

To update after new commits: `git pull && docker compose up -d --build`.
Logs: `docker compose logs -f checkers-server`.

### As a TrueNAS SCALE app

TrueNAS apps pull a ready-made image instead of building from source. The `Docker image` GitHub
Actions workflow publishes one to GitHub Container Registry whenever server code changes:

| Branch | Image |
|---|---|
| `main` | `ghcr.io/vexsent/checkers-server:latest` |
| `Staging` | `ghcr.io/vexsent/checkers-server:staging` |

Each build is also tagged `sha-<commit>`, so you can pin or roll back to an exact build.

In TrueNAS: Apps → Discover Apps → Install via YAML, then paste (and fill in the two values):

```yaml
services:
  checkers-server:
    image: ghcr.io/vexsent/checkers-server:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__CheckersDbfallback: "Server=<sql-server-ip>,1433;Database=Checkers;User Id=<user>;Password=<password>;TrustServerCertificate=True"
      AppOptions__Jwt__SigningKey: "<openssl rand -base64 48>"
```

- To update, redeploy the app after the workflow finishes; it pulls the new image.
- If SQL Server runs on the same TrueNAS box, use the TrueNAS IP, not `localhost`: inside the
  container, `localhost` is the container itself.
- If the package is private (GitHub → your profile → Packages → `checkers-server` → Package
  settings), TrueNAS needs registry credentials: a GitHub token with `read:packages`.

Reverse proxy:
- Forward `checkers.nartleb.app` to `http://<server-ip>:8080`.
- Turn on WebSocket support. Live matches use SignalR, which needs WebSockets.
- The client's production `BaseUrl` (`Client/Resources/Raw/appsettings.json`) is `https://checkers.nartleb.app/`.
