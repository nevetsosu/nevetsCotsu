FROM ubuntu:latest

ARG DISCORD_TOKEN
ENV DISCORD_TOKEN ${DISCORD_TOKEN}

WORKDIR /app

RUN apt update && apt upgrade -y
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet run .