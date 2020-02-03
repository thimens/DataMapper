using System;
using Thimens.DataMapper;
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
            var db = DatabaseProviderFactory.Create(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=D:\Projetos\Repos\Thimens.DataMapper\Test\Database.mdf;Integrated Security=True;Connect Timeout=30", "SQL");
            dynamic nameOf = new NameOf<Client>();

            Console.WriteLine("Naming product name property:");
            Console.WriteLine($"ToString: {nameOf.orders.products.name}");
            Console.WriteLine($"ToSQL: {nameOf.orders.products.name.ToSQL}");
            Console.WriteLine($"ToDB2: {nameOf.orders.products.name.ToDB2}");
            Console.WriteLine("");


            var query = @$"select c.id, c.name, o.Id {nameOf.Orders.Id.ToSQL}, o.deliveryTime {nameOf.Orders.DeliveryTime.ToSQL}, p.productId {nameOf.Orders.Products.Id.ToSQL}, p.name {nameOf.orders.Products.Name.ToSQL}, p.value {nameOf.orders.Products.Value.ToSQL} " +
                "from client c inner join [order] o " +
                        "on c.id = o.clientId " + 
                    "inner join order_product p " +
                        "on o.id = p.orderId " +
                "where c.id = 1";

            var client = db.Get<Client>(CommandType.Text, query, null, (string)nameOf.orders.id, (string)nameOf.orders.products.id);

            Console.WriteLine("---------------------------");
            Console.WriteLine($"ID: {client.ID}");
            Console.WriteLine($"Name: {client.Name}");
            Console.WriteLine($"Orders:");

            foreach (var order in client.Orders)
            {
                Console.WriteLine($"  ID: {order.ID}");
                Console.WriteLine($"  Delivery: {order.DeliveryTime}");
                Console.WriteLine($"  Products:");

                foreach (var product in order.Products)
                {
                    Console.WriteLine($"    ID: {product.ID}");
                    Console.WriteLine($"    Name: {product.Name}");
                    Console.WriteLine($"    Value: {product.Value}");
                    Console.WriteLine("");
                }
            }

            // example no2

            query = @$"select c.id, c.name, o.Id [{nameOf.ordersid}.id] " +
                "from client c inner join [order] o " +
                        "on c.id = o.clientId " +
                    "inner join order_product p " +
                        "on o.id = p.orderId " +
                "where c.id = 1";

            client = db.Get<Client>(CommandType.Text, query, null);

            query = @"select c.id, c.name, o.Id ""ordersid.id"" " +
               "from client c inner join [order] o " +
                       "on c.id = o.clientId " +
                   "inner join order_product p " +
                       "on o.id = p.orderId " +
               "where c.id = 0";

            client = db.Get<Client>(CommandType.Text, query, null);

            query = @"select c.id, c.name, o.Id ""ordersid.id"" " +
               "from client c left join [order] o " +
                       "on c.id = o.clientId " +
                   "left join order_product p " +
                       "on o.id = p.orderId " +
               "where c.id = 9";

            client = db.Get<Client>(CommandType.Text, query, null);

            Console.WriteLine("---------------------------");
            Console.WriteLine($"ID: {client.ID}");
            Console.WriteLine($"Name: {client.Name}");
            Console.WriteLine($"Orders:");

            foreach (var orderID in client.OrdersID)
                Console.WriteLine($"  ID: {orderID}");

            // exmaple no3

            query = @"select count(*) from client" ;

            var count = db.Get<int>(CommandType.Text, query, null);

            Console.WriteLine("---------------------------");
            Console.WriteLine($"Client count: {count}");

            // example no4

            query = $@"select c.id, c.name, o.Id [{nameOf.ordersid}.id] " +
                "from client c inner join [order] o " +
                        "on c.id = o.clientId " +
                    "inner join order_product p " +
                        "on o.id = p.orderId " +
                "where c.id in (1, 2)";

            var clients = db.Get<IEnumerable<Client>>(CommandType.Text, query, null, (string)nameOf.id, $"{nameOf.ordersid}.id");

            Console.WriteLine("---------------------------");

            foreach (var cl in clients)
            {
                Console.WriteLine($"ID: {cl.ID}");
                Console.WriteLine($"Name: {cl.Name}");
                Console.WriteLine($"Orders:");

                foreach (var orderID in cl.OrdersID)
                    Console.WriteLine($"  ID: {orderID}");
            }
            // example no5

            query = $@"select c.id, c.name, o.Id [{nameOf.orders.id}], o.deliveryTime [{nameOf.orders.deliverytime}], p.productId [{nameOf.orders.products.id}], p.name [{nameOf.orders.products.name}], p.value [{nameOf.orders.products.value}] " +
                "from client c inner join [order] o " +
                        "on c.id = o.clientId " +
                    "inner join order_product p " +
                        "on o.id = p.orderId " +
                "where c.id in (1, 2)";

            clients = db.Get<IEnumerable<Client>>(CommandType.Text, query, null, (string)nameOf.id, (string)nameOf.orders.id, (string)nameOf.orders.products.id);

            Console.WriteLine("---------------------------");

            foreach (var cl in clients)
            {
                Console.WriteLine($"ID: {cl.ID}");
                Console.WriteLine($"Name: {cl.Name}");
                Console.WriteLine($"Orders:");

                foreach (var order in cl.Orders)
                {
                    Console.WriteLine($"  ID: {order.ID}");
                    Console.WriteLine($"  Delivery: {order.DeliveryTime}");
                    Console.WriteLine($"  Products:");

                    foreach (var product in order.Products)
                    {
                        Console.WriteLine($"    ID: {product.ID}");
                        Console.WriteLine($"    Name: {product.Name}");
                        Console.WriteLine($"    Value: {product.Value}");
                        Console.WriteLine("");
                    }
                }
            }

            // example no6

            query = @"select p.productid from order_product p";

            var prods = db.Get<IEnumerable<int>>(CommandType.Text, query, null);

            Console.WriteLine("---------------------------");
            Console.WriteLine($"Products: ");
            foreach (var productID in prods)
                Console.WriteLine($"  {productID}");

            Console.ReadKey();
        }
    }
}
