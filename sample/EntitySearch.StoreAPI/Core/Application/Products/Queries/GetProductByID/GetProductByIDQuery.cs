﻿using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntitySearch.StoreAPI.Core.Application.Products.Queries.GetProductByID
{
    public class GetProductByIDQuery : IRequest<GetProductByIDQueryResponse>
    {
        public int ProductID { get; set; }
    }
}
