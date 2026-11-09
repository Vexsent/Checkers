# Builds the Checkers server (ASP.NET Core API + SignalR game hub).
# The MAUI client isn't part of this image; it connects to the server over HTTPS.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from the project files alone so the package layer is reused when only code changes.
COPY Shared/Shared.csproj Shared/
COPY Domain/Domain.csproj Domain/
COPY Persistence/Persistence.csproj Persistence/
COPY Application/Application.csproj Application/
COPY Server/Server.csproj Server/
RUN dotnet restore Server/Server.csproj

COPY Shared/ Shared/
COPY Domain/ Domain/
COPY Persistence/ Persistence/
COPY Application/ Application/
COPY Server/ Server/
RUN dotnet publish Server/Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Plain HTTP on 8080 behind a reverse proxy that handles HTTPS. Forwarded headers let the app
# see the original https scheme and client address from the proxy.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080

# Non-root user that ships with the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Server.dll"]
