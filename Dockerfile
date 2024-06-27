FROM ubuntu:latest
WORKDIR /app

ARG DISCORD_TOKEN
ENV DISCORD_TOKEN ${DISCORD_TOKEN}

RUN apt update
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0 -y
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

ENTRYPOINT dotnet run .