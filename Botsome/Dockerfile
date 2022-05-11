FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Botsome.csproj", "./"]
RUN dotnet restore "Botsome.csproj"
COPY . .
RUN dotnet build "Botsome.csproj" -c Release -o /app/build && \
    dotnet publish "Botsome.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS run
WORKDIR /app
EXPOSE 80

# Add an unprivileged user to run the app as.
# Also add curl for the healthcheck. I'd like to remove the world-execute permission on curl, but the
# healthcheck is run as the unprivileged user.
# TODO move part of this file into a published image, with a setuid binary that executes the healthcheck
RUN groupadd --gid 2000 app && \
    useradd --uid 1000 --gid 2000 app && \
    chown app:app /app && \
    apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y curl

HEALTHCHECK --start-period=5s \
    # Get the first character of the http response code, and make sure it's not 5. (400 errors are fine) \
    CMD test $(curl --silent -I http://localhost | head -n 1 | cut -d " "  -f 2 | cut -b 1) -ne 5

COPY --from=build /app/publish .

USER app:app
    
ENTRYPOINT ["dotnet", "Botsome.dll"]
