﻿# syntax = docker/dockerfile:experimental

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
ARG RID=x64

WORKDIR /src
COPY ["JSDXTS.csproj", "./"]
RUN dotnet restore "JSDXTS.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "JSDXTS.csproj" -c Release -o /app/build
    
FROM build AS publish
RUN dotnet publish "JSDXTS.csproj" -c Release -o /app/publish \
    --arch $RID \
    --os alpine \
    --self-contained true \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true

#https://m.imooc.com/article/316499
#https://docs.microsoft.com/zh-cn/dotnet/core/rid-catalog
#https://hub.docker.com/_/microsoft-dotnet-runtime-deps

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine AS final
ARG version=0.0.0

LABEL version=${version}
LABEL name="js.189.cn-speedup"
LABEL url="https://hub.docker.com/repository/docker/lpyedge/js.189.cn-speedup"
LABEL email="lpyedge#163.com"

WORKDIR /app
COPY --from=publish /app/publish .
ENV TZ=Asia/Shanghai
ENV interval=0

#包安装源切换到阿里云
#修复alpine时区设置的问题
RUN sed -i 's/dl-cdn.alpinelinux.org/mirrors.aliyun.com/g' /etc/apk/repositories; \
    apk --update add tzdata;

#进程状态检查
COPY healthcheck.sh .
RUN chmod -R 777 /app/healthcheck.sh
HEALTHCHECK --interval=15m --timeout=5s --start-period=30s CMD /app/healthcheck.sh JSDXTS

ENTRYPOINT ["/app/JSDXTS"]