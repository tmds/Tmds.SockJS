FROM microsoft/aspnet:1.0.0-beta7

RUN apt-get update \
        && apt-get install -y --force-yes git \
        && rm -rf /var/lib/apt/lists/*

RUN git clone https://github.com/tmds/Tmds.SockJS.git

WORKDIR /Tmds.SockJS/
RUN MONO_THREADS_PER_CPU=2000 dnu restore

WORKDIR /Tmds.SockJS/test/WebSites/TestWebSite/
EXPOSE 8081
CMD ["dnx", "kestrel"]