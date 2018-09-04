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

Remember these dll are for the unmanaged (native) version not the managed one, that means that when compiling you need to specify either x64 or x86 in Visual Studio otherwise you'll get a BadImageException.

Note: This are for the Oracle 12.1 Client, they vary depending on the client version and the functionality you want to use.


#### Use Guide

The library tries to simplify the use of Oracle UDTs by allowing the use of simple .NET CLR POCOs an not having to deal with all the requirements set by the Oracle.DataAccess library, it does this by creating an in-memory dll filled with Proxies that do all the required work.

A simple example, lets say you have the following Oracle UDT object:
```sql
CREATE TYPE oe.order_item_typ AS OBJECT
(
	order_id    NUMBER(12),
	order_name  VARCHAR(35),
	quantity    NUMBER(4)
);
```

in C# a mapped object would look like this:

```csharp
[UDTName("oe.order_item_typ")]
public class OrderItem
{
    [UDTProperty("order_id")]
	public long Id { get; set; }

	[UDTProperty("order_name")]
	public string Name { get; set; }

	public int quantity { get; set; }
}
```

Lets say you have have a collection (or varray) of the type above like this:

```sql
CREATE TYPE oe.order_item_list_typ AS TABLE OF oe.order_item_typ;
```

you just need to add the `UDTCollectionNameAttribute` to the POCO above:

```csharp
[UDTName("oe.order_item_typ")]
[UDTCollectionName("oe.order_item_list_typ")]
public class OrderItem
{
	...
}
```

But what happens when you don't have a base POCO type, for example if you have a collection like this:

```sql
CREATE TYPE oe.product_ref_list_typ AS TABLE OF number(6); 
```

For types like this you need to use the `Proxy` utility class, that can be used to create proxies at runtime, for example:

```csharp
Proxy.CreateListType<int>("oe.product_ref_list_typ");
```
<br>

### Calling Oracle Packages / Procedure / Functions

In order to execute an Oracle Package, procedure or function you will need to wrap your parameters in `Param` objects, and call the `ServiceForOracle` class either through your own class that inherits from it or by using the `DefaultServiceForOracle` implementation.

Using the POCO created above you could execute the following example function as follows:

```sql
CREATE OR REPLACE FUNCTION oe.TestFunction(dummy oe.order_item_typ)
	RETURNS oe.order_item_list_typ
IS
	returnList oe.order_item_list_typ := oe.order_item_list_typ();
BEGIN
	returnList.Extend;
	returnList(returnList.Last) := oe.order_item_typ(1, "Test 1", 10);
	returnList.Extend;
	returnList(returnList.Last) := oe.order_item_typ(2, "Test 2", 20);

	return returnList;
END TestFunction;
```

```csharp
static async Task Main(string[] args)
{
    var serv = new DefaultServiceForOracle("Data Source=IP:PORT/ORCL; Pooling=True;User id=; password=;");

    var parameter = Param.Input(new OrderItem { Id = 1 });

    var orderList = serv.ExecuteFunctionAsync<OrderItem[]>("oe.TestFunction", parameter);

	foreach(var order in orderList)
	{
		Console.WriteLine(order.Name);
	}
}
```