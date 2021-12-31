#!/bin/sh

ProcessName="$1"

if [ "$(ps -o comm | grep -c $ProcessName)" -eq 1 ]; then
  #echo "[$ProcessName]进程运行状态 : 正常"
  exit 0
else
  #echo "[$ProcessName]进程运行状态 : 错误"
  exit 1
fi