FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY ["JSDXTS.csproj", "./"]
RUN dotnet restore "JSDXTS.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "JSDXTS.csproj" -c Release -o /app/build
    
FROM build AS publish
RUN dotnet publish "JSDXTS.csproj" -c Release  -o /app/publish \
    --self-contained false 

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS final
MAINTAINER "lpyedge"
LABEL version="0.x.x"
LABEL name="js.189.cn-speedup"
LABEL url="https://hub.docker.com/repository/docker/lpyedge/js.189.cn-speedup"
LABEL email="lpyedge#163.com"

WORKDIR /app
COPY --from=publish /app/publish .
ENV TZ=Asia/Shanghai
ENV interval=30

#修复alpine时区设置的问题
RUN sed -i 's/dl-cdn.alpinelinux.org/mirrors.aliyun.com/g' /etc/apk/repositories; \
    apk --update add tzdata;

#进程状态检查
COPY healthcheck.sh .
RUN chmod -R 777 /app/healthcheck.sh
HEALTHCHECK --interval=15m --timeout=5s --start-period=30s CMD /app/healthcheck.sh JSDXTS

ENTRYPOINT ["/app/JSDXTS"]