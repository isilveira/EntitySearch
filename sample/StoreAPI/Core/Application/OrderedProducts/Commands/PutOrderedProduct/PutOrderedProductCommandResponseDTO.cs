﻿using System;

namespace StoreAPI.Core.Application.OrderedProducts.Commands.PutOrderedProduct
{
    public class PutOrderedProductCommandResponseDTO
    {
        public int OrderedProductID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }

        public int Amount { get; set; }
        public decimal Value { get; set; }

        public DateTime RegistrationDate { get; set; }
    }
}
