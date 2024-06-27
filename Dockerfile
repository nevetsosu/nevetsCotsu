FROM ubuntu:latest

ARG DISCORD_TOKEN
ENV DISCORD_TOKEN ${DISCORD_TOKEN}

WORKDIR /app

RUN sudo apt update
RUN sudo apt install libopus-dev 
RUN sudo apt ffmpeg
RUN sudo apt dotnet-sdk-8.0
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet run .