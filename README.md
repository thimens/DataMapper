# DataMapper
A object mapper for .NET. It extends Database class of Enterprise Library Data Block.

## Overview
---
With these extensions, you can fill your classes directly from database.
You have these methods:
 * ExecuteScalar
 * ExecuteNonQuery
 * Get<T>
 * List<T>

The **ExecuteScalar** and **ExecuteNonQuery** are just extensions of original methods of Database class, but now accepting new parameters. The first one  returns the first column of the first row in the result set returned by a query, and the last one returns the numbers of rows affected by the query.

The **Get<T>** method returns a single object filled with data from database. The **List<T>** method returns a list of objects.

## Get method
---
Use this method to fill a single object from database. 

### The class
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
### Filling the class
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
