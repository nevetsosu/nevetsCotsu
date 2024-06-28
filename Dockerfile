FROM ubuntu:latest
WORKDIR /app
RUN mkdir /app/bin
ENV PATH "$PATH:/app/bin"

# initial dependencies
RUN apt update
RUN apt install wget libopus-dev ffmpeg dotnet-sdk-8.0 -y

# yt-dlp
RUN wget https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -O /app/bin/yt-dlp
RUN chmod a+rx /app/bin/yt-dlp  # Make executable
RUN printenv PATH

COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet build --configuration Release .

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "."]