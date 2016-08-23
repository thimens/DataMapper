# DataMapper
A object mapper for .NET. It extends Database class of Enterprise Library Data Block.

## Overview
With these extensions, you can fill your classes directly from database.
There are these four methods:
 * ExecuteScalar
 * ExecuteNonQuery
 * Get\<T>
 * List\<T>

The **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.

The **Get\<T>** method returns a single object filled with data from database. The **List\<T>** method returns a list of objects.

## Get method
Use this method to fill a single object from database. To do so, the fields of the result set returned by the query need to have the same name of properties of the object that you want to fill (not case sensitive). Properties and fields with different names are ignored.  
You can use the **Parameter** class to add parameters to your query. If it is not necessary, just use *null* in the call.

### Basic usage
The class:
```c#
public class Order
{
  public int Id { get; set; }
  public string ClientName { get; set; }
  public DateTime DeliveryDate { get; set; }
  public decimal Freight { get; set; }
  public decimal ProductsValue { get; set; }
}
```
The code:
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = "select OrderNumber as Id, ClientName, DtDelivery as DeliveryDate, Freight from Order where OrderNumber = @OrderNumber";

var parameters = List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters);
```
And voilá! Your `Order` class with Id, DeliveryDate and Freight filled from database.

### Nested objects
The classes:
```c#
public class Order
{
  public int Id { get; set; }
  public string ClientName { get; set; }
  public DateTime DeliveryDate { get; set; }
  public decimal Freight { get; set; }
  public decimal ProductsValue { get; set; }
  public Address Address { get; set; }
}

public class Address
{
  public string Street { get; set; }
  public string Number { get; set; }
  public string Complement { get; set; }
  public string City { get; set; }
  public string State { get; set; }
  public int Zip { get; set; }
}
```
The code:
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = @"select OrderNumber as Id, ClientName, Street as ""Address.Street"", Number as ""Address.Number"", zip as ""Address.Zip""
from Order where OrderNumber = @OrderNumber";

var parameters = List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters);
```
For deeper levels of nested objects, just continue to use periods (.) in column alias of your query.  
`"MainProperty.Level1Property.Level2Property.Level3Property...(and so on)"`  
For example:  
`@"select s.Name as ""Order.Store.Name"" from Order o inner join Store s on o.StoreId = s.Id where o.OrderNumber = @OrderNumber"`  
Or:  
`@"select c.AddrZip as ""Order.Client.Address.Zip"" from Order o inner join Client c on o.ClientId = c.Id where o.OrderNumber = @OrderNumber"`

### Nested Lists
