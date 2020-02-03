# DataMapper
An easy-to-use and high-performance object mapper (micro ORM) for .NET.

Nugget package [here](https://www.nuget.org/packages/Thimens.DataMapper).

Just match the fields name of your query (or procedure) with the properties tree of your class, and, if you're using lists, inform their key(s). And that's it! Sweet freshly read data mapped directly to your class. No extra coding necessary.

The project's been built over a modified code of the Database class (ok, and few more classes) of [Enterprise Library Data Access Application Block](https://msdn.microsoft.com/en-us/library/microsoft.practices.enterpriselibrary.data.database(v=pandp.60).aspx) to run over .NET Standard 2.0.

## Overview
The **Get\<T>** method returns T with data from database (lists and nested lists included!). 

The **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.

Obs: The **List\<T>** method is deprecated. To return a list from database, use Get\<U> instead, where U is a list of T, e.g., Get\<IEnumerable\<T>> or Get\<ICollection\<T>>

## Get\<T> method
Use this method to return a T object from database. To do so, the columns name of the result set must match the properties name of the object that you want to set (case-insensitive). Properties and columns with different names are ignored.  
You can use the **Parameter** class to add parameters to your query. If it is not necessary, just use *null* in the call.

### Basic usage
The first step is register your [factories](https://docs.microsoft.com/en-us/dotnet/api/system.data.common.dbproviderfactory?view=netstandard-2.0):
```c#
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance);
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL"); //factory alias
```
Then create the Database object:
```c#
//if no alias was informed, you can use factory type name or factory type. otherwise, you must use the alias.
var db = DatabaseProviderFactory.Create(connectionString, typeof(SqlClientFactory)); 
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //using factory alias.
```

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
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = "select OrderNumber as Id, ClientName, DtDelivery as DeliveryDate, Freight from Order where OrderNumber = @OrderNumber";

var parameters = new List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters);
```
And voilá! Your `Order` class with `Id`, `ClientName`, `DeliveryDate` and `Freight` properties set from database.

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
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = @"select OrderNumber as Id, ClientName, Street as ""Address.Street"", Number as ""Address.Number"", zip as ""Address.Zip""
from Order where OrderNumber = @OrderNumber";

var parameters = new List<Parameter>();
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
Pretty much like nested objects. With nested lists, you have the option to inform the property(ies) that will be used as key to add to the list (like a single or composite primary key in a database table).

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
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = @"select o.OrderNumber as Id, o.ClientName, p.Id as ""Products.Id"", p.Quantity as ""Products.Quantity"", p.Name as ""Products.Name"" from Order o inner join OrderProduct p on o.OrderNumber = p.OrderNumber where o.OrderNumber = @OrderNumber";

var parameters = new List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters, "Products.Id");
```
The last parameter `"Products.Id"` of **Get\<T>** method means: For `Products` list, use property `Id` as key. You can inform as properties as necessary. For example, if you have a list of `Clients`, and the keys of list are the properties `FirstName` and `LastName`, you must do a call like this:
```c#
return db.Get<ExampleClass>(CommandType.Text, query, parameters, "Clients.FirstName", "Clients.LastName");
```
If you don't inform any key, each row read from database will be added as a new item to the list. Sometimes it is ok, sometimes it is not. I recommend you to inform the keys whenever possible. 

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

public class Class
{
  public int Id { get; set; }
  public string Name { get; set; }
}
```
The code:
```c#
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = @"select sc.Id, sc.Name, st.Id ""Students.Id"", st.Name ""Students.Name"", c.Id ""Students.Classes.Id"", c.Name ""Students.Classes.Name"" from School sc inner join Students st on sc.Id = st.SchoolId inner join StudentClass c on c.StudentId = st.Id  where sc.Id = @SchoolId";

var parameters = new List<Parameter>();
parameters.Add(new Parameter("@SchoolId", DbType.Int32, schoolId));

return db.Get<Order>(CommandType.Text, query, parameters, "Students.Id", "Students.Classes.Id");
```
The `Classes` list of a student will be created only with classes of that specific student.

You can use nested objects and nested lists at the same time without any problem.

### Returning lists
To return a list of T class from database, use **Get\<U>**, where U is a list of T, e.g., **Get\<IEnumerable\<T>>** or **Get\<ICollection\<T>>**. Just like nested lists, you can inform the key(s) of the list.

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
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = "select OrderNumber as Id, ClientName, DtDelivery as DeliveryDate, Freight from Order";

return db.Get<IEnumerable<Order>>(CommandType.Text, query, null, "Id"); //no parameters
```
The property `Id` will be used as key to add to the list of orders.

### Lists and DBNull

If a list item value or a list item key (if you have specified any) is DBNull, the list item is dismissed and not included in the list.

## Special conversions
There are two special conversions that you can use:  

1.You can set a enum property of your object from a string column of database, if your enum values have the `DefaultValue` attribute on them.  
For example, if you have the following values on `Status` column of the `Subscription` table: `"P"` (Paused), `"A"` (Active), `"I"` (Idle):  

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
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = @"select Id, ClientName, Status from Subscription where id = @id";

var parameters = new List<Parameter>();
parameters.Add(new Parameter("@id", DbType.Int32, id));

return db.Get<Subscription>(CommandType.Text, query, parameters);
```

2.You can set a bool property from a string column, if this column has `"Y" - "N"` values.  


## ExecuteScalar and ExecuteNonQuery
As mentioned, **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.
```c#
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = "select count(Id) from Order";

return (int)db.ExecuteScalar(CommandType.Text, query, null); //no parameters -- returns the value of 'count(Id)'
```
```c#
DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
var db = DatabaseProviderFactory.Create(connectionString, "SQL"); //create a new Database object from connection string and factory alias

var query = "update Student set (firstName, lastName) = (@firstName, @lastName) where id = @id";

var parameters = new List<Parameter>();
parameters.Add(new Parameter("@id", DbType.Int32, id));
parameters.Add(new Parameter("@firstName", DbType.String, firstName));
parameters.Add(new Parameter("@lastName", DbType.String, lastName));

return db.ExecuteNonQuery(CommandType.Text, query, parameters); //returns 1
```
