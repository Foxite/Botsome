FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Botsome.Coordinator/Botsome.Coordinator.csproj", "Botsome.Coordinator/"]
RUN dotnet restore "Botsome.Coordinator/Botsome.Coordinator.csproj"
COPY . .
WORKDIR "/src/Botsome.Coordinator"
RUN dotnet build "Botsome.Coordinator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Botsome.Coordinator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Botsome.Coordinator.dll"]
