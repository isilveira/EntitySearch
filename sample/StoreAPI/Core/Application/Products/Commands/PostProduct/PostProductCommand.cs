﻿using MediatR;
using ModelWrapper;
using StoreAPI.Core.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoreAPI.Core.Application.Products.Commands.PostProduct
{
    public class PostProductCommand : Wrap<Product>, IRequest<PostProductCommandResponse>
    {
        public PostProductCommand()
        {
            KeyProperty(x => x.ProductID);
            SuppressProperty(x => x.RegistrationDate);
            SuppressProperty(x => x.Category);
            SuppressProperty(x => x.Images);
            SuppressProperty(x => x.OrderedProducts);
        }
    }
}
