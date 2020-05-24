#!/bin/bash

nuget restore

# 64-bit
#cp ../libAttract/libAttract.0.95.5/Windows/Release/x64/libAttract.dll .
#msbuild VFatumbot.sln -p:Configuration=Release -p:Platform=x64 -p:DeployOnBuild=true -p:PublishProfile=vfatumbot-Web-Deploy.pubxml -p:Password=d8eohGdoftYeKjiGjWCdTnZo92QE7dlyxtjmokwjTwzRbrJh415hTn0bGbWP
#msbuild VFatumbot.sln -p:Configuration=Release -p:Runtime=win-x64 -p:DeployOnBuild=true -p:PublishProfile=vfatumbot-Web-Deploy.pubxml -p:Password=d8eohGdoftYeKjiGjWCdTnZo92QE7dlyxtjmokwjTwzRbrJh415hTn0bGbWP

# 32-bit
cp ../libAttract/libAttract.0.95.5/Windows/Release/Win32/libAttract.dll .
msbuild VFatumbot.sln -p:DeployOnBuild=true -p:PublishProfile=vfatumbot-Web-Deploy.pubxml -p:Password=d8eohGdoftYeKjiGjWCdTnZo92QE7dlyxtjmokwjTwzRbrJh415hTn0bGbWP
#cp ../libAttract/libAttract.0.95.5/Windows/Debug/x64/libAttract.dll .
