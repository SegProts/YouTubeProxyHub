# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости
COPY ["YouTubeProxyHub.csproj", "./"]
RUN dotnet restore "YouTubeProxyHub.csproj"

# Копируем всё остальное и собираем
COPY . .
RUN dotnet publish "YouTubeProxyHub.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Render передает порт через переменную PORT, .NET подхватит его через ASPNETCORE_URLS
ENTRYPOINT ["dotnet", "YouTubeProxyHub.dll"]