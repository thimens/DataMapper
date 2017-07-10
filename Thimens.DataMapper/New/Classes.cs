using System;
using System.Collections.Generic;
using System.Text;

namespace ClassLibrary1
{
    public class Client
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public IEnumerable<Order> Orders{ get; set; }
    }

    public class Order
    {
        public int ID { get; set; }
        public DateTime DeliveryTime { get; set; }
        public IEnumerable<Product> Products { get; set; }
    }

    public class Product
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public decimal Value { get; set; }
    }
}
