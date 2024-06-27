FROM ubuntu:latest
WORKDIR /app

# initial dependencies
RUN apt update
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0 -y

# yt-dlp
RUN add-apt-repository ppa:tomtomtom/yt-dlp
RUN apt update
RUN apt install yt-dlp

COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet build --configuration Release .

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "."]