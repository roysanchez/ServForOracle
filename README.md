This is a small library that improves the Oracle Object (UDT) handling on the Windows native Oracle Client framework.

The library depends on the native ODP.NET client because is the only one that handles Oracle's UDT.

#### Installation Guide

Oracle doesn't publish the native version on nuget so you'll need to get it through their Portal, I recommend downloading the ODAC Runtime (http://www.oracle.com/technetwork/topics/dotnet/downloads/odacdeploy-4242173.html) for the bitness of your dev PC and your deployment server.

The only file that you need to reference is Oracle.DataAccess, but in order for it to work your app needs to be able to find the following dlls:
* oci.dll
* oraocci12.dll
* oraociei12.dll
* oraons.dll
* OraOps12.dll

Note: This are for the Oracle 12.1 Client, they vary depending on the client version and the functionality you want to use.

