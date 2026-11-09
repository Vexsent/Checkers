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

Reverse proxy:
- Forward `checkers.nartleb.app` to `http://<server-ip>:8080`.
- Turn on WebSocket support. Live matches use SignalR, which needs WebSockets.
- The client's production `BaseUrl` (`Client/Resources/Raw/appsettings.json`) is `https://checkers.nartleb.app/`.
