﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntitySearch.StoreAPI.Core.Application.Products.Commands.PutProduct
{
    public class PutProductCommandResponseItem
    {
        public int ProductID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime RegistrationDate { get; set; }
        public decimal Value { get; set; }
        public int Ammount { get; set; }
        public bool IsPublic { get; set; }

        public PutProductCommandResponseItem()
        {

        }
    }
}
