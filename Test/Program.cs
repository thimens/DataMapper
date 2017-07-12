using System;
using Thimens.DataMapper;
using Thimens.DataMapper.New;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            DatabaseProviderFactory.RegisterFactory(SqlClientFactory.Instance, "SQL");
            var db = DatabaseProviderFactory.Create(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\lc001093\Source\Repos\Thimens.DataMapper\Test\Database.mdf;Integrated Security=True;Connect Timeout=30", "SQL");

            var query = @"select c.id, c.name, o.Id ""orders.id"", o.deliveryTime ""orders.deliverytime"", p.productId ""orders.products.id"", p.name ""orders.products.name"", p.value ""orders.products.value"" " +
                "from client c inner join [order] o " +
                        "on c.id = o.clientId " + 
                    "inner join order_product p " +
                        "on o.id = p.orderId " +
                "where c.id = 1";

            var client = db.Get<Client>(CommandType.Text, query, null, "orders.id", "orders.products.id");

            query = @"select c.id, c.name, o.Id ""ordersid.id"" " +
                "from client c inner join [order] o " +
                        "on c.id = o.clientId " +
                    "inner join order_product p " +
                        "on o.id = p.orderId " +
                "where c.id = 1";

            client = db.Get<Client>(CommandType.Text, query, null, "ordersid.id");
        }
    }
}
