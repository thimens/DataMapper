# DataMapper
A object mapper for .NET. It extends Database class of Enterprise Library Data Block.

## Overview
With these extensions methods, you can fill your classes directly from database.
There are:
 * ExecuteScalar
 * ExecuteNonQuery
 * Get\<T>
 * List\<T>

The **Get\<T>** method returns a single object filled with data from database. The **List\<T>** method returns a list of objects.

The **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.

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
Pretty much like nested objects. With nested lists, you have the option to inform the property(ies) that will be used as key(s) to fill the list (like a single or composite primary key in a database table).

The classes:
```c#
public class Order
{
  public int Id { get; set; }
  public string ClientName { get; set; }
  public decimal Freight { get; set; }
  public List<Product> Products { get; set; }
}

public class Product
{
  public int Id { get; set; }
  public int Quantity { get; set; }
  public string Name { get; set; }
}
```
The code:
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = @"select o.OrderNumber as Id, o.ClientName, p.Id as ""Products.Id"", p.Quantity as ""Products.Quantity"", p.Name as ""Products.Name"" from Order o inner join OrderProduct p on o.OrderNumber = p.OrderNumber where o.OrderNumber = @OrderNumber";

var parameters = List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters, "Products.Id");
```
The last parameter `"Products.Id"` of **Get\<T>** method means: For `Products` list, use property `Id` as key. You can inform as properties as necessary. For example, if you have a list of `Clients`, and the keys of list are the properties `FirstName` and `LastName`, you must make a call like this:
```c#
return db.Get<ExampleClass>(CommandType.Text, query, parameters, "Clients.FirstName", "Clients.LastName");
```
If you don't inform any key, all items read from database will be added to the list. Sometimes it is ok, sometimes it is not. I recommend you to inform the keys always as possible. 

Like nested objects, you can also nest a list inside another list.    
The classes:
```c#
public class School
{
  public int Id { get; set; }
  public string Name { get; set; }
  public List<Student> Students { get; set; }
}

public class Student
{
  public int Id { get; set; }
  public string Name { get; set; }
  public List<Class> Classes { get; set; }
}

public class Student
{
  public int Id { get; set; }
  public string Name { get; set; }
}
```
The code:
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = @"select sc.Id, sc.Name, st.Id ""Students.Id"", st.Name ""Students.Name"", c.Id ""Students.Classes.Id"", c.Name ""Students.Classes.Name"" from School sc inner join Students st on sc.Id = st.SchoolId inner join StudentClass c on c.StudentId = st.Id  where sc.Id = @SchoolId";

var parameters = List<Parameter>();
parameters.Add(new Parameter("@SchoolId", DbType.Int32, schoolId));

return db.Get<Order>(CommandType.Text, query, parameters, "Students.Id", "Students.Classes.Id");
```

You can use nested objects and nested lists at the same time.

## List method
Use just like **Get\<T>** method, but like nested lists, you can inform the properties used as keys.
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

var query = "select OrderNumber as Id, ClientName, DtDelivery as DeliveryDate, Freight from Order";

return db.List<Order>(CommandType.Text, query, null, "Id"); //no parameters
```
The property `Id` will be used as key to fill the list of orders.

## Special conversions
There are two special conversions that you can use:
1.You can fill a enum property in your class from a string column of database, if your enum values have the `DefaultValue` attribute on them.  
For example, if you have the following values on status column of a subscription table: "P" (Paused), "A" (Active), "I" (Idle)
The class:
```c#
public class Subscription
{
  public int Id { get; set; }
  public string ClientName { get; set; }
  public Status Status { get; set; }
}

public enum Status
{
  [DefaultValue("P")] Paused,
  [DefaultValue("A")] Active,
  [DefaultValue("I")] Idle
}
```
The code:
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = @"select Id, ClientName, Status from Subscription where id = @id";

var parameters = List<Parameter>();
parameters.Add(new Parameter("@id", DbType.Int32, id));

return db.Get<Subscription>(CommandType.Text, query, parameters);
```

2.You can fill a bool property in your class from a string column of database, if this column is a 'Y' - 'N' field.  



## ExecuteScalar and ExecuteNonQuery
As said before, **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = "select count(Id) from Order";

return (int)db.ExecuteScalar(CommandType.Text, query, null); //no parameters
```
```c#
var db = new DatabaseProviderFactory().Create("DbConnection"); //create a new Database object

var query = "update Student set (firstName, lastName) = (@firstName, @lastName) where id = @id";

var parameters = List<Parameter>();
parameters.Add(new Parameter("@id", DbType.Int32, id));
parameters.Add(new Parameter("@firstName", DbType.String, firstName));
parameters.Add(new Parameter("@lastName", DbType.String, lastName));

return db.ExecuteNonQuery(CommandType.Text, query, parameters);
```
