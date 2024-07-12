FROM ubuntu:latest
WORKDIR /app
RUN mkdir /app/bin
ENV PATH="$PATH:/app/bin"

# initial dependencies
RUN apt update
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0 -y

# restore dependencies
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
RUN dotnet restore .

# copy in app binaries
COPY bin/Release/net8.0/* ./
CMD ["./dotnetDiscordBot"]