FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Botsome/Botsome.csproj", "./Botsome/Botsome.csproj"]

RUN dotnet restore "Botsome/Botsome.csproj"
COPY . .
WORKDIR "/src/Botsome"
RUN dotnet build "Botsome.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish "Botsome.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS final

WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
CMD ["dotnet", "Botsome.dll"]
