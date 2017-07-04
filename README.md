# DataMapper
A easy-to-use and powerfull object mapper for .NET. 

Go to the database once with a simple (or complex, it is up to you) query and have your objects (lists included!), and their nested objects, and their nested objects, and... (again, you're the boss! you decide when stop it) filled with sweet and freshly read data.
It's been build over a modified code of the Database class (and few more classes) of [Enterprise Library Data Access Application Block](https://msdn.microsoft.com/en-us/library/microsoft.practices.enterpriselibrary.data.database(v=pandp.60).aspx) to run over .NET Standard 2.0.

Nugget package [here]() (awaiting .NET Standard 2.0 became stable to publish)


## Overview
With these methods, you can fill your objects directly from database.
They are:
 * Get\<T>
 * List\<T>
 * ExecuteScalar
 * ExecuteNonQuery

The **Get\<T>** method returns a single object filled with data from database. The **List\<T>** method returns a list of objects.

The **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of IDbCommand, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.

## Get\<T> method
Use this method to fill a single object from database. To do so, the columns name of the result set must match the properties name of the object that you want to fill (case-insensitive). Properties and columns with different names are ignored.  
You can use the **Parameter** class to add parameters to your query. If it is not necessary, just use *null* in the call.

### Basic usage
The first step is register your [DbProviderFactory](https://docs.microsoft.com/en-us/dotnet/api/system.data.common.dbproviderfactory?view=netstandard-2.0):
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

var parameters = List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters);
```
And voilá! Your `Order` class with `Id`, `ClientName`, `DeliveryDate` and `Freight` properties filled from database.

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
Pretty much like nested objects. With nested lists, you have the option to inform the property(ies) that will be used as key to fill the list (like a single or composite primary key in a database table).

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

var parameters = List<Parameter>();
parameters.Add(new Parameter("@OrderNumber", DbType.Int32, orderNumber));

return db.Get<Order>(CommandType.Text, query, parameters, "Products.Id");
```
The last parameter `"Products.Id"` of **Get\<T>** method means: For `Products` list, use property `Id` as key. You can inform as properties as necessary. For example, if you have a list of `Clients`, and the keys of list are the properties `FirstName` and `LastName`, you must do a call like this:
```c#
return db.Get<ExampleClass>(CommandType.Text, query, parameters, "Clients.FirstName", "Clients.LastName");
```
If you don't inform any key, all items read from database will be added to the list. Sometimes it is ok, sometimes it is not. I recommend you to inform the keys whenever possible. 

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

var parameters = List<Parameter>();
parameters.Add(new Parameter("@SchoolId", DbType.Int32, schoolId));

return db.Get<Order>(CommandType.Text, query, parameters, "Students.Id", "Students.Classes.Id");
```
The `Classes` list of a student will be filled only with classes of that specific student.

You can use nested objects and nested lists at the same time without any problem.

#### Special case for list key
In some rare cases, you may have a key for a list that is not a property of the list item, but a property of a property of the item. In these cases, you can inform this type of key with `@` sign. For example, the key `"Volumes.Sector@Id"` means: the property `Id` of property `Sector` of each volume in `Volumes` list will be used as key. The classes hierarchy that describe this case is:
```c#
public class MainClass
{
  public List<Volume> Volumes { get; set; }
}

public class Volume
{
  public decimal Weight { get; set; }
  public Sector Sector { get; set; }
}

public class Sector
{
  public int Id { get; set; }
  public string Name { get; set; }
}
```
And code:
```c#
return db.Get<MainClass>(CommandType.Text, query, parameters, "Volumes.Sector@Id"); //returns a object MainClass with a list of volumes inside it
```
Or:
```c#
return db.List<Volume>(CommandType.Text, query, parameters, "Sector@Id"); //returns a list of volumes directly
```
Therefore, when filling the `Volumes` list, the property `Id` of property `Sector` will be checked to validate if the item is already in the list. 

## List\<T> method
Use just like **Get\<T>** method, but like nested lists, you can inform the properties used as key.
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

return db.List<Order>(CommandType.Text, query, null, "Id"); //no parameters
```
The property `Id` will be used as key to fill the list of orders.

## Special conversions
There are two special conversions that you can use:  
1.You can fill a enum property of your object from a string column of database, if your enum values have the `DefaultValue` attribute on them.  
For example, if you have the following values on `Status` column of a `Subscription` table: `"P"` (Paused), `"A"` (Active), `"I"` (Idle)  
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

var parameters = List<Parameter>();
parameters.Add(new Parameter("@id", DbType.Int32, id));

return db.Get<Subscription>(CommandType.Text, query, parameters);
```
2.You can fill a bool property of your object from a string column, if this column has `"Y" - "N"` values.  



## ExecuteScalar and ExecuteNonQuery
As said before, **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.
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

var parameters = List<Parameter>();
parameters.Add(new Parameter("@id", DbType.Int32, id));
parameters.Add(new Parameter("@firstName", DbType.String, firstName));
parameters.Add(new Parameter("@lastName", DbType.String, lastName));

return db.ExecuteNonQuery(CommandType.Text, query, parameters); //returns 1
```
