﻿using EntitySearch.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntitySearch
{
    public static class EntitySearchExtensions
    {
        public static IQueryable<TSource> Search<TSource>(this IQueryable<TSource> source, IFilter<TSource> filter)
            where TSource : class
        {
            if (string.IsNullOrWhiteSpace(filter.Query))
            {
                return source;
            }

            var queryTokens = BreakQuery(filter.Query);
            var criteriaExp = GenerateSearchCriteriaExpression<TSource>(queryTokens);

            return source.Where(criteriaExp);
        }
        public static IQueryable<TSource> OrderBy<TSource>(this IQueryable<TSource> source, IFilter<TSource> filter)
            where TSource : class
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            Type type = typeof(TSource);
            LambdaExpression lambda = GenereteLambdaExpression<TSource>(ref type, filter.OrderBy);
            MethodInfo orderMethod;

            if (filter.Order == Order.ASCENDING)
            {
                orderMethod = GetMethodFromType(typeof(Queryable), "OrderBy", 2, 2);
            }
            else
            {
                orderMethod = GetMethodFromType(typeof(Queryable), "OrderByDescending", 2, 2);
            }

            return (IQueryable<TSource>)orderMethod.MakeGenericMethod(typeof(TSource), type).Invoke(null, new object[] { source, lambda });
        }
        public static IQueryable<TSource> Scope<TSource>(this IQueryable<TSource> source, IFilter<TSource> filter)
            where TSource : class
        {
            return source.Skip(filter.PageNumber * filter.PageSize).Take(filter.PageSize);
        }
        public static IQueryable<TSource> Count<TSource>(this IQueryable<TSource> source, ref int count)
        {
            count = source.Count();
            return source;
        }
        public static IQueryable<TSource> CountLong<TSource>(this IQueryable<TSource> source, ref long count)
        {
            count = source.Count();
            return source;
        }
        private static List<string> BreakQuery(string query)
        {
            return query.ToLower().Split(" ").ToList();
        }
        private static MethodInfo GetMethodFromType(Type type, string methodName, int parameters, int genericArguments, List<Type> parameterTypes = null)
        {
            return type.GetMethods()
                .SingleOrDefault(method =>
                    method.Name == methodName
                    && method.GetParameters().Count() == parameters
                    && method.GetGenericArguments().Count() == genericArguments
                    && (parameterTypes == null || parameterTypes.All(x => method.GetParameters().Select(parameter => parameter.ParameterType).Contains(x)))
                );
        }
        private static Expression<Func<TSource, bool>> GenerateSearchCriteriaExpression<TSource>(List<string> tokens)
        {
            List<Expression> expressions = new List<Expression>();

            var xExp = Expression.Parameter(typeof(TSource), "x");

            foreach (var propertyInfo in typeof(TSource).GetProperties())
            {
                Expression memberExp = Expression.MakeMemberAccess(xExp, propertyInfo);

                if (memberExp.Type != typeof(string))
                {
                    memberExp = Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "ToString", 0, 0));
                }

                memberExp = Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "ToLower", 0, 0));

                foreach (var token in tokens)
                {
                    var tokenExp = Expression.Constant(token, typeof(string));
                    var containsExp = Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "Contains", 1, 0, new List<Type> { typeof(string) }), tokenExp);
                    expressions.Add(containsExp);
                }
            }

            Expression orExp = Expression.Empty();
            if (expressions.Count == 1)
            {
                orExp = expressions[0];
            }
            else
            {
                orExp = Expression.Or(expressions[0], expressions[1]);

                for (int i = 2; i < expressions.Count; i++)
                {
                    orExp = Expression.Or(orExp, expressions[i]);
                }
            }

            return Expression.Lambda<Func<TSource, bool>>(orExp.Reduce(), xExp);
        }
        private static LambdaExpression GenereteLambdaExpression<T>(ref Type type, string propertyName)
        {
            ParameterExpression arg = Expression.Parameter(type, "x");
            List<string> listProperties = propertyName.Split('.').ToList();

            Expression expr = arg;
            foreach (string item in listProperties)
            {
                PropertyInfo np = type.GetProperties().SingleOrDefault(x => x.Name.ToLower() == item.ToLower());
                expr = Expression.MakeMemberAccess(expr, np);
                type = np.PropertyType;
            }

            Type delegateType = typeof(Func<,>).MakeGenericType(typeof(T), type);
            return Expression.Lambda(delegateType, expr, arg);
        }
    }
}
