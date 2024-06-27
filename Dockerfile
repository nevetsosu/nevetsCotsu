FROM ubuntu:latest
WORKDIR /app

RUN apt update
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0 -y
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet build --configuration Release .

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "."]