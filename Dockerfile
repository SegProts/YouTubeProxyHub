FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Установка Python, Curl и Node.js (для JS runtime)
RUN apt-get update && apt-get install -y python3 curl nodejs

# Скачивание yt-dlp
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp

COPY ["YouTubeProxyHub.csproj", "./"]
RUN dotnet restore "YouTubeProxyHub.csproj"
COPY . .
RUN dotnet publish "YouTubeProxyHub.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Установка Python и Node.js в финальный образ
RUN apt-get update && apt-get install -y python3 nodejs && rm -rf /var/lib/apt/lists/*
COPY --from=build /usr/local/bin/yt-dlp /usr/local/bin/yt-dlp

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YouTubeProxyHub.dll"]