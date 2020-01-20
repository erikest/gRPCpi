dotnet publish ../src/gRPCpi.Client/gRPCpi.Client.csproj -r linux-arm 

scp -rp ../src/gRPCpi.Client/bin/Debug/netcoreapp3.1/linux-arm/publish/ pi@192.168.1.180:~/gRPCpi_Client