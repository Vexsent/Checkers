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
- The proxy must send `X-Forwarded-For`. Rate limits count anonymous requests per client IP, and
  without that header every visitor looks like the proxy and shares one allowance.
- Only the proxy should reach port 8080. The server trusts `X-Forwarded-For` from whoever connects,
  so a client that can reach 8080 directly can claim any IP.

Rate limits (per fixed window; signed-in users are counted per account, everyone else per IP):

| Limit | Applies to | Default |
|---|---|---|
| `Auth` | Login and register, per IP | 10 per 60 s |
| `Api` | Every other HTTP request | 120 per 60 s |
| `Lobby` | Creating and joining live matches | 10 per 60 s |
| `Game` | Moves, resigning and draw offers | 30 per 10 s |

Over a limit, HTTP requests get `429 Too Many Requests` with a `Retry-After` header, and live-match
calls return "Too many requests. Please wait a moment and try again." `/health` and static files
are not limited. To change a limit, add an environment variable to `docker-compose.yml`, e.g.
`AppOptions__RateLimiting__Auth__PermitLimit: 5` or `AppOptions__RateLimiting__Game__WindowSeconds: 20`.
