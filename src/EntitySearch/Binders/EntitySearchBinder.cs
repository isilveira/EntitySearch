﻿using EntitySearch.Interfaces;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntitySearch.Binders
{
    public class EntitySearchBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            bindingContext.Model = Activator.CreateInstance(bindingContext.ModelType);

            foreach (var property in ((IEntitySearch)bindingContext.Model).GetSearchableProperties(bindingContext.ModelType.GetProperties()))
            {
                var valueProviderResult = bindingContext.ValueProvider.GetValue(property.Name);
                if (valueProviderResult != ValueProviderResult.None)
                {
                    if (property.Name == "QueryProperties")
                    {
                        if (valueProviderResult.Length > 1)
                        {
                            valueProviderResult.Values.ToList().ForEach(value => ((IEntitySearch)bindingContext.Model).QueryProperties.Add(value));
                        }
                        else
                        {
                            ((IEntitySearch)bindingContext.Model).QueryProperties.Add(valueProviderResult.FirstValue);
                        }
                    }
                    else if (property.Name == "Order")
                    {
                        property.SetValue(bindingContext.Model, Convert.ToInt32(valueProviderResult.FirstValue));
                    }
                    else
                    {
                        property.SetValue(bindingContext.Model, Convert.ChangeType(valueProviderResult.FirstValue, property.PropertyType));
                    }

                    bindingContext.ModelState.SetModelValue(property.Name, valueProviderResult);
                }
                else
                {
                    if (property.Name == "FilterProperties")
                    {
                        foreach (var filterProperty in ((IEntitySearch)bindingContext.Model).GetSearchableProperties(bindingContext.ModelType.BaseType.GenericTypeArguments[0].GetProperties()))
                        {
                            GetPropertyTypeBinders(filterProperty.PropertyType).ForEach(typeBinder =>
                            {
                                var filterName = $"{filterProperty.Name}{typeBinder}";
                                valueProviderResult = bindingContext.ValueProvider.GetValue(filterName);
                                if (valueProviderResult != ValueProviderResult.None)
                                {
                                    var listObjects = new List<object>();
                                    valueProviderResult.Values.ToList().ForEach(value =>
                                    {
                                        bool changed = false;
                                        object typedValue = TryChangeType(value, filterProperty.PropertyType, ref changed);
                                        if (changed)
                                        {
                                            listObjects.Add(typedValue);
                                        }
                                    });

                                    if (listObjects.Count > 0)
                                    {
                                        ((IEntitySearch)bindingContext.Model).FilterProperties.Add(filterName, listObjects.Count > 1 ? listObjects : listObjects.FirstOrDefault());
                                    }
                                }
                            });
                        }
                    }
                }
            }

            if (bindingContext.Model == null)
            {
                return Task.CompletedTask;
            }

            bindingContext.Result = ModelBindingResult.Success(bindingContext.Model);
            return Task.CompletedTask;
        }

        private List<string> GetPropertyTypeBinders(Type propertyType)
        {
            List<string> comparationTypes = new List<string>();

            if (Nullable.GetUnderlyingType(propertyType)!=null)
            {
                propertyType = Nullable.GetUnderlyingType(propertyType);
            }

            comparationTypes.Add("");
            comparationTypes.Add("_Not");
            if (propertyType == typeof(string) || propertyType == typeof(char))
            {
                comparationTypes.Add("_Contains");
                comparationTypes.Add("_NotContains");
                comparationTypes.Add("_StartsWith");
                comparationTypes.Add("_NotStartsWith");
                comparationTypes.Add("_EndsWith");
                comparationTypes.Add("_NotEndsWith");
            }

            if (propertyType == typeof(int)
                    || propertyType == typeof(long)
                    || propertyType == typeof(float)
                    || propertyType == typeof(float)
                    || propertyType == typeof(double)
                    || propertyType == typeof(decimal)
                    || propertyType == typeof(DateTime)
                    || propertyType == typeof(TimeSpan))
            {
                comparationTypes.Add("_GreaterThan");
                comparationTypes.Add("_GreaterThanOrEqual");
                comparationTypes.Add("_LessThan");
                comparationTypes.Add("_LessThanOrEqual");
            }

            return comparationTypes.Distinct().ToList();
        }

        private object TryChangeType(string value, Type typeTo, ref bool changed)
        {
            try
            {
                if (Nullable.GetUnderlyingType(typeTo) != null)
                {
                    if (!string.IsNullOrWhiteSpace(value) && value == "null")
                    {
                        changed = true;
                        return Activator.CreateInstance(typeTo);
                    }
                    else
                    {
                        typeTo = Nullable.GetUnderlyingType(typeTo);
                    }
                }

                object convertedObject = Convert.ChangeType(value, typeTo);
                changed = true;
                return convertedObject;
            }
            catch (Exception ex)
            {
                changed = false;
                return Activator.CreateInstance(typeTo);
            }
        }
    }
}
