FROM ubuntu:latest
WORKDIR /app
RUN mkdir /app/bin
ENV PATH="$PATH:/app/bin"

# initial dependencies
RUN apt update
RUN apt install libopus-dev ffmpeg -y

# copy in app binaries
COPY bin/Release/net8.0/* .
CMD ["./dotnetDiscordBot"]