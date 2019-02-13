﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntitySearch.StoreAPI.Core.Application.Products.Queries.GetProductsByFilter
{
    public class GetProductsByFilterQueryResponse
    {
        public GetProductsByFilterQuery Request { get; set; }
        public int ResultCount { get; set; }
        public List<GetProductsByFilterQueryItemResponse> Results { get; set; }
    }
}
