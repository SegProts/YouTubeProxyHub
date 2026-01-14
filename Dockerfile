# ЭТАП СБОРКИ
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 1. Устанавливаем зависимости (Python и Curl) для работы yt-dlp
RUN apt-get update && apt-get install -y python3 curl

# 2. Скачиваем актуальную версию yt-dlp для Linux
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp

# 3. Даем права на запуск
RUN chmod a+rx /usr/local/bin/yt-dlp

# 4. Сборка .NET проекта
COPY ["YouTubeProxyHub.csproj", "./"]
RUN dotnet restore "YouTubeProxyHub.csproj"
COPY . .
RUN dotnet publish "YouTubeProxyHub.csproj" -c Release -o /app/publish

# ФИНАЛЬНЫЙ ОБРАЗ
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Копируем yt-dlp и Python в финальный контейнер
COPY --from=build /usr/local/bin/yt-dlp /usr/local/bin/yt-dlp
RUN apt-get update && apt-get install -y python3 && rm -rf /var/lib/apt/lists/*

# Копируем опубликованное приложение
COPY --from=build /app/publish .

# Запуск
ENTRYPOINT ["dotnet", "YouTubeProxyHub.dll"]