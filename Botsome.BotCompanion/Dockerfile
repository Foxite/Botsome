FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Botsome.BotCompanion/Botsome.BotCompanion.csproj", "Botsome.BotCompanion/"]
RUN dotnet restore "Botsome.BotCompanion/Botsome.BotCompanion.csproj"
COPY . .
WORKDIR "/src/Botsome.BotCompanion"
RUN dotnet build "Botsome.BotCompanion.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Botsome.BotCompanion.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Botsome.BotCompanion.dll"]
