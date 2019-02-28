﻿using EntitySearch.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntitySearch.Extensions
{
    public static class EntitySearchExtensions
    {
        public static IQueryable<TSource> Filter<TSource>(this IQueryable<TSource> source, IFilter<TSource> filter)
            where TSource : class
        {
            if (filter.FilterProperties == null || filter.FilterProperties.Count == 0)
            {
                return source;
            }

            var criteriaExp = GenerateFilterCriteriaExpression(filter);

            return source.Where(criteriaExp);
        }
        public static IQueryable<TSource> Search<TSource>(this IQueryable<TSource> source, IFilter<TSource> filter)
            where TSource : class
        {
            if (string.IsNullOrWhiteSpace(filter.Query))
            {
                return source;
            }

            var queryTokens = BreakQuery(filter.Query, filter.QueryPhrase);
            var criteriaExp = GenerateSearchCriteriaExpression(queryTokens, filter);

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
            Expression lambda = GenereteLambdaExpression<TSource>(ref type, filter.OrderBy);
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
        private static Expression<Func<TSource, bool>> GenerateFilterCriteriaExpression<TSource>(IFilter<TSource> filter) where TSource : class
        {
            List<Expression> expressions = new List<Expression>();

            var xExp = Expression.Parameter(typeof(TSource), "x");

            foreach (var filterProperty in filter.FilterProperties)
            {
                var propertyParts = filterProperty.Key.Split("_");
                var property = typeof(TSource).GetProperty(propertyParts[0]);

                Expression memberExp = Expression.MakeMemberAccess(xExp, property);

                if (propertyParts.Count() == 1)
                {
                    expressions.Add(GenerateFilterComparationExpression(memberExp, filterProperty, property));
                }
                else
                {
                    expressions.Add(GenerateFilterComparationExpression(memberExp, filterProperty, property, propertyParts[1]));
                }
            }

            Expression orExp = GenerateAndExpressions(expressions);

            return Expression.Lambda<Func<TSource, bool>>(orExp.Reduce(), xExp);
        }
        private static Expression<Func<TSource, bool>> GenerateSearchCriteriaExpression<TSource>(IList<string> tokens, IFilter<TSource> filter)
            where TSource : class
        {
            List<Expression> orExpressions = new List<Expression>();

            var xExp = Expression.Parameter(typeof(TSource), "x");

            foreach (var propertyInfo in GetPropertiesFromType(filter.GetSearchableProperties(typeof(TSource).GetProperties()), filter.QueryProperties))
            {
                Expression memberExp = Expression.MakeMemberAccess(xExp, propertyInfo);

                if (memberExp.Type != typeof(string))
                {
                    memberExp = Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "ToString", 0, 0));
                }

                memberExp = Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "ToLower", 0, 0));
                List<Expression> andExpressions = new List<Expression>();
                foreach (var token in tokens)
                {
                    andExpressions.Add(GenerateStringContainsExpression(memberExp, Expression.Constant(token, typeof(string))));
                }
                orExpressions.Add(filter.QueryStrict ? GenerateAndExpressions(andExpressions) : GenerateOrExpression(andExpressions));
            }

            Expression orExp = GenerateOrExpression(orExpressions);

            return Expression.Lambda<Func<TSource, bool>>(orExp.Reduce(), xExp);
        }
        private static Expression GenerateFilterComparationExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property, string comparation = null)
        {
            if (string.IsNullOrWhiteSpace(comparation))
            {
                return GenerateFilterStrictExpression(memberExp, filterProperty, property);
            }
            if (comparation == "Not")
            {
                return Expression.Not(GenerateFilterStrictExpression(memberExp, filterProperty, property));
            }
            if (comparation == "Contains")
            {
                return GenerateFilterContainsExpression(memberExp, filterProperty, property);
            }
            if (comparation == "NotContains")
            {
                return Expression.Not(GenerateFilterContainsExpression(memberExp, filterProperty, property));
            }
            if (comparation == "StartsWith")
            {
                return GenerateFilterStartsWithExpression(memberExp, filterProperty, property);
            }
            if (comparation == "NotStartsWith")
            {
                return Expression.Not(GenerateFilterStartsWithExpression(memberExp, filterProperty, property));
            }
            if (comparation == "EndsWith")
            {
                return GenerateFilterEndsWithExpression(memberExp, filterProperty, property);
            }
            if (comparation == "NotEndsWith")
            {
                return Expression.Not(GenerateFilterEndsWithExpression(memberExp, filterProperty, property));
            }
            if (comparation == "GreaterThan")
            {
                return GenerateFilterGreaterThanExpression(memberExp, filterProperty, property);
            }
            if (comparation == "GreaterThanOrEqual")
            {
                return GenerateFilterGreaterEqualExpression(memberExp, filterProperty, property);
            }
            if (comparation == "LessThan")
            {
                return GenerateFilterSmallerThanExpression(memberExp, filterProperty, property);
            }
            if (comparation == "LessThanOrEqual")
            {
                return GenerateFilterSmallerEqualExpression(memberExp, filterProperty, property);
            }
            return Expression.Empty();
        }
        private static Expression GenerateFilterStrictExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(Expression.Equal(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return Expression.Equal(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterContainsExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(GenerateStringContainsExpression(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return GenerateStringContainsExpression(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterStartsWithExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(GenerateStringStartsWithExpression(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return GenerateStringStartsWithExpression(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterEndsWithExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(GenerateStringEndsWithExpression(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return GenerateStringEndsWithExpression(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterGreaterThanExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(Expression.GreaterThan(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return Expression.GreaterThan(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterGreaterEqualExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(Expression.GreaterThanOrEqual(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return Expression.GreaterThanOrEqual(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterSmallerThanExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(Expression.LessThan(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return Expression.LessThan(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateFilterSmallerEqualExpression(Expression memberExp, KeyValuePair<string, object> filterProperty, PropertyInfo property)
        {
            if (filterProperty.Value.GetType().IsGenericType && filterProperty.Value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                List<Expression> orExpressions = new List<Expression>();
                foreach (var value in ((List<object>)filterProperty.Value))
                {
                    orExpressions.Add(Expression.LessThanOrEqual(memberExp, Expression.Constant(value, property.PropertyType)));
                }
                return GenerateOrExpression(orExpressions);
            }
            else
            {
                return Expression.LessThanOrEqual(memberExp, Expression.Constant(filterProperty.Value, property.PropertyType));
            }
        }
        private static Expression GenerateStringContainsExpression(Expression memberExp, ConstantExpression tokenExp)
        {
            return Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "Contains", 1, 0, new List<Type> { typeof(string) }), tokenExp);
        }
        private static Expression GenerateStringStartsWithExpression(Expression memberExp, ConstantExpression tokenExp)
        {
            return Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "StartsWith", 1, 0, new List<Type> { typeof(string) }), tokenExp);
        }
        private static Expression GenerateStringEndsWithExpression(Expression memberExp, ConstantExpression tokenExp)
        {
            return Expression.Call(memberExp, GetMethodFromType(memberExp.Type, "EndsWith", 1, 0, new List<Type> { typeof(string) }), tokenExp);
        }
        private static Expression GenerateAndExpressions(List<Expression> expressions)
        {
            Expression andExp = Expression.Empty();
            if (expressions.Count == 1)
            {
                andExp = expressions[0];
            }
            else
            {
                andExp = Expression.And(expressions[0], expressions[1]);

                for (int i = 2; i < expressions.Count; i++)
                {
                    andExp = Expression.And(andExp, expressions[i]);
                }
            }
            return andExp;
        }
        private static Expression GenerateOrExpression(List<Expression> expressions)
        {
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
            return orExp;
        }
        private static Expression GenereteLambdaExpression<T>(ref Type type, string propertyName)
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
        private static IList<PropertyInfo> GetPropertiesFromType(IList<PropertyInfo> searchableProperties, IList<string> queryProperties = null)
        {
            return searchableProperties.Where(x => queryProperties == null || (queryProperties != null && queryProperties.Count == 0) || queryProperties.Any(y => y.ToLower() == x.Name.ToLower())).ToList();
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
        private static IList<string> BreakQuery(string query, bool queryPhrase)
        {
            return queryPhrase ? new List<string> { query } : query.ToLower().Split(" ").ToList();
        }
    }
}