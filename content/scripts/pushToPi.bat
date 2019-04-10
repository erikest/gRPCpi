dotnet publish ../gRPCpi/src/gRPCpi.Client/gRPCpi.Client.csproj -r linux-arm 

scp -rp ../gRPCpi/src/gRPCpi.Client/bin/Debug/netcoreapp3.0/linux-arm/publish/ pi@192.168.1.180:~/gRPCpi_Client