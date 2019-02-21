﻿using EntitySearch.Interfaces;
using EntitySearch.StoreAPI.Core.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntitySearch.StoreAPI.Core.Application.Products.Queries.GetProductsByFilter
{
    public class GetProductsByFilterQuery : IRequest<GetProductsByFilterQueryResponse>, IFilter<Product>
    {
        public string Query { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string OrderBy { get; set; }
        public Order Order { get; set; }

        public GetProductsByFilterQuery()
        {
            PageNumber = 0;
            PageSize = 10;
            OrderBy = "ProductID";
            Order = Order.ASCENDING;
        }
    }
}
