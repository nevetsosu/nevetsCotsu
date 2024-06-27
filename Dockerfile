FROM ubuntu:latest
WORKDIR /app

RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .
COPY .env .

RUN dotnet run .