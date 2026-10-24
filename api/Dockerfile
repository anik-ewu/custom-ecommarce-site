# API image for Render (or any container host). Build from the repo root:
#   docker build -f api/Dockerfile -t kinbo-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so code changes don't invalidate the package layer.
COPY api/nuget.config api/Directory.Build.props api/Shop.sln ./
COPY api/Shop.Api/Shop.Api.csproj Shop.Api/
COPY api/Shop.Application/Shop.Application.csproj Shop.Application/
COPY api/Shop.Domain/Shop.Domain.csproj Shop.Domain/
COPY api/Shop.Infrastructure/Shop.Infrastructure.csproj Shop.Infrastructure/
RUN dotnet restore Shop.Api/Shop.Api.csproj

COPY api/Shop.Api/ Shop.Api/
COPY api/Shop.Application/ Shop.Application/
COPY api/Shop.Domain/ Shop.Domain/
COPY api/Shop.Infrastructure/ Shop.Infrastructure/
RUN dotnet publish Shop.Api/Shop.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Render sends traffic to $PORT (10000 by default); fall back to 8080 elsewhere.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
# The aspnet image ships a non-root "app" user. /app stays root-owned; only the dev-only local uploads
# folder is writable, so the compose `full` profile can run as Development without Cloudinary.
RUN mkdir -p /app/App_Data/uploads && chown "$APP_UID" /app/App_Data/uploads
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet Shop.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
