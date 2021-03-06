﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Test
{
    public class Client
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public IEnumerable<Order> Orders{ get; set; }
        public IEnumerable<int> OrdersID { get; set; }
    }

    public class Order
    {
        public int ID { get; set; }
        public DateTime DeliveryTime { get; set; }
        public IEnumerable<Product> Products { get; set; }
        public Status Status { get; set; }
    }

    public class Product
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public decimal Value { get; set; }
    }

    public enum Status
    {
        Created,
        Started,
        Finished
    }
}
