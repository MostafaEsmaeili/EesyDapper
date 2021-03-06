﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EasyDapper.Core.Abstractions;

namespace EasyDapper.Core
{
    public class DataReaderEntityMapper : IEntityMapper
    {
        private static readonly Dictionary<string, object> EntityMapperDefinitions = new Dictionary<string, object>();

        public virtual IEnumerable<TEntity> Map<TEntity>(IDataReader reader) where TEntity : class, new()
        {
            return MapList<TEntity>(reader);
        }

        public List<TEntity> MapList<TEntity>(IDataReader reader) where TEntity : class, new()
        {
            var entityList = new List<TEntity>();
            var fullName = typeof(TEntity).FullName;
            EntityMapperDefinition<TEntity> mapperDefinition;
            if (EntityMapperDefinitions.ContainsKey(fullName))
            {
                mapperDefinition = (EntityMapperDefinition<TEntity>) EntityMapperDefinitions[fullName];
            }
            else
            {
                mapperDefinition = new EntityMapperDefinition<TEntity>
                {
                    Activator = EntityActivator.GetActivator<TEntity>()
                };
                foreach (var property in typeof(TEntity).GetProperties())
                {
                    mapperDefinition.PropertySetters.Add(property.Name, BuildUntypedSetter<TEntity>(property));
                    mapperDefinition.ColumnTypeMappings.Add(property.Name, property.PropertyType);
                }

                EntityMapperDefinitions.Add(fullName, mapperDefinition);
            }

            var flag = true;
            var funcArray = new Func<IDataReader, TEntity, TEntity>[reader.FieldCount];
            while (reader.Read())
            {
                var entity1 = mapperDefinition.Activator();
                if (flag)
                {
                    flag = false;
                    for (var i = 0; i < reader.FieldCount; ++i)
                    {
                        var columnName = reader.GetName(i);
                        var keyValuePair = mapperDefinition.PropertySetters
                            .Where(p => p.Key.ToLower() == columnName.ToLower()).FirstOrDefault();
                        if (keyValuePair.Key != null)
                        {
                            columnName = keyValuePair.Key;
                            var propertySetter = mapperDefinition.PropertySetters[columnName];
                            var fieldIndex = i;
                            if (mapperDefinition.ColumnTypeMappings.ContainsKey(columnName))
                            {
                                var columnTypeMapping = mapperDefinition.ColumnTypeMappings[columnName];
                                if (columnTypeMapping == typeof(decimal) || columnTypeMapping == typeof(decimal?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetDecimal(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(string))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetString(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(short) || columnTypeMapping == typeof(short?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetInt16(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(int) || columnTypeMapping == typeof(int?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetInt32(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(long) || columnTypeMapping == typeof(long?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetInt64(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(DateTime) ||
                                         columnTypeMapping == typeof(DateTime?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetDateTime(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(double) || columnTypeMapping == typeof(double?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetDouble(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(bool) || columnTypeMapping == typeof(bool?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetBoolean(fieldIndex));
                                        return e;
                                    };
                                }
                                else if (columnTypeMapping == typeof(byte) || columnTypeMapping == typeof(byte?))
                                {
                                    funcArray[i] = (dataReader, e) =>
                                    {
                                        if (reader.IsDBNull(fieldIndex))
                                            return e;
                                        propertySetter(e, reader.GetByte(fieldIndex));
                                        return e;
                                    };
                                }
                                else
                                {
                                    var num = columnTypeMapping == typeof(float) ? 1 :
                                        columnTypeMapping == typeof(float?) ? 1 : 0;
                                    funcArray[i] = num == 0
                                        ? (dataReader, e) =>
                                        {
                                            if (reader.IsDBNull(fieldIndex))
                                                return e;
                                            propertySetter(e, reader.GetValue(fieldIndex));
                                            return e;
                                        }
                                        : (Func<IDataReader, TEntity, TEntity>) ((dataReader, e) =>
                                        {
                                            if (reader.IsDBNull(fieldIndex))
                                                return e;
                                            propertySetter(e, reader.GetFloat(fieldIndex));
                                            return e;
                                        });
                                }
                            }
                            else
                            {
                                funcArray[i] = (dataReader, e) =>
                                {
                                    if (reader.IsDBNull(fieldIndex))
                                        return e;
                                    propertySetter(e, reader.GetValue(fieldIndex));
                                    return e;
                                };
                            }
                        }
                    }
                }

                foreach (var func in funcArray)
                    if (func != null)
                    {
                        var entity2 = func(reader, entity1);
                    }

                entityList.Add(entity1);
            }

            return entityList;
        }

        public TLEntity MapEntityList<TLEntity, T>(IDataReader reader)
            where TLEntity : List<T>, new()
            where T : class, new()
        {
            var lentity = new TLEntity();
            var fullName = typeof(T).FullName;
            EntityMapperDefinition<T> mapperDefinition;
            if (EntityMapperDefinitions.ContainsKey(fullName))
            {
                mapperDefinition = (EntityMapperDefinition<T>) EntityMapperDefinitions[fullName];
            }
            else
            {
                mapperDefinition = new EntityMapperDefinition<T>
                {
                    Activator = EntityActivator.GetActivator<T>()
                };
                foreach (var property in typeof(T).GetProperties())
                {
                    mapperDefinition.PropertySetters.Add(property.Name, BuildUntypedSetter<T>(property));
                    mapperDefinition.ColumnTypeMappings.Add(property.Name, property.PropertyType);
                }

                EntityMapperDefinitions.Add(fullName, mapperDefinition);
            }

            var flag = true;
            var funcArray = new Func<IDataReader, T, T>[reader.FieldCount];
            while (reader.Read())
            {
                var obj1 = mapperDefinition.Activator();
                if (flag)
                {
                    flag = false;
                    for (var i = 0; i < reader.FieldCount; ++i)
                    {
                        var name = reader.GetName(i);
                        if (mapperDefinition.PropertySetters.ContainsKey(name))
                        {
                            var propertySetter = mapperDefinition.PropertySetters[name];
                            var fieldIndex = i;
                            if (mapperDefinition.ColumnTypeMappings.ContainsKey(name))
                            {
                                var columnTypeMapping = mapperDefinition.ColumnTypeMappings[name];
                                funcArray[i] = !(columnTypeMapping == typeof(decimal))
                                    ? !(columnTypeMapping == typeof(decimal?))
                                        ?
                                        !(columnTypeMapping == typeof(string)) ? !(columnTypeMapping == typeof(short))
                                            ?
                                            !(columnTypeMapping == typeof(short?))
                                                ?
                                                !(columnTypeMapping == typeof(int))
                                                    ?
                                                    !(columnTypeMapping == typeof(int?))
                                                        ?
                                                        !(columnTypeMapping == typeof(long))
                                                            ?
                                                            !(columnTypeMapping == typeof(long?))
                                                                ?
                                                                !(columnTypeMapping == typeof(DateTime))
                                                                    ?
                                                                    !(columnTypeMapping == typeof(DateTime?))
                                                                        ?
                                                                        !(columnTypeMapping == typeof(double))
                                                                            ?
                                                                            !(columnTypeMapping == typeof(double?))
                                                                                ?
                                                                                !(columnTypeMapping == typeof(bool))
                                                                                    ?
                                                                                    !(columnTypeMapping == typeof(bool?)
                                                                                        ) ? !(columnTypeMapping ==
                                                                                              typeof(byte))
                                                                                        ?
                                                                                        !(columnTypeMapping ==
                                                                                          typeof(byte?))
                                                                                            ?
                                                                                            !(columnTypeMapping ==
                                                                                              typeof(float))
                                                                                                ?
                                                                                                !(columnTypeMapping ==
                                                                                                  typeof(float?))
                                                                                                    ?
                                                                                                    (dataReader, e) =>
                                                                                                    {
                                                                                                        if (reader
                                                                                                            .IsDBNull(
                                                                                                                fieldIndex)
                                                                                                        )
                                                                                                            return e;
                                                                                                        propertySetter(
                                                                                                            e,
                                                                                                            reader
                                                                                                                .GetValue(
                                                                                                                    fieldIndex));
                                                                                                        return e;
                                                                                                    }
                                                                                                    : (Func<IDataReader,
                                                                                                        T, T>) ((
                                                                                                        dataReader,
                                                                                                        e) =>
                                                                                                    {
                                                                                                        if (reader
                                                                                                            .IsDBNull(
                                                                                                                fieldIndex)
                                                                                                        )
                                                                                                            return e;
                                                                                                        propertySetter(
                                                                                                            e,
                                                                                                            reader
                                                                                                                .GetFloat(
                                                                                                                    fieldIndex));
                                                                                                        return e;
                                                                                                    })
                                                                                                : (dataReader, e) =>
                                                                                                {
                                                                                                    propertySetter(e,
                                                                                                        reader.GetFloat(
                                                                                                            fieldIndex));
                                                                                                    return e;
                                                                                                }
                                                                                            : (dataReader, e) =>
                                                                                            {
                                                                                                if (reader.IsDBNull(
                                                                                                    fieldIndex))
                                                                                                    return e;
                                                                                                propertySetter(e,
                                                                                                    reader.GetByte(
                                                                                                        fieldIndex));
                                                                                                return e;
                                                                                            }
                                                                                        : (dataReader, e) =>
                                                                                        {
                                                                                            propertySetter(e,
                                                                                                reader.GetByte(
                                                                                                    fieldIndex));
                                                                                            return e;
                                                                                        } : (dataReader, e) =>
                                                                                    {
                                                                                        if (reader.IsDBNull(fieldIndex))
                                                                                            return e;
                                                                                        propertySetter(e,
                                                                                            reader.GetBoolean(
                                                                                                fieldIndex));
                                                                                        return e;
                                                                                    }
                                                                                    : (dataReader, e) =>
                                                                                    {
                                                                                        propertySetter(e,
                                                                                            reader.GetBoolean(
                                                                                                fieldIndex));
                                                                                        return e;
                                                                                    }
                                                                                : (dataReader, e) =>
                                                                                {
                                                                                    if (reader.IsDBNull(fieldIndex))
                                                                                        return e;
                                                                                    propertySetter(e,
                                                                                        reader.GetDouble(fieldIndex));
                                                                                    return e;
                                                                                }
                                                                            : (dataReader, e) =>
                                                                            {
                                                                                propertySetter(e,
                                                                                    reader.GetDouble(fieldIndex));
                                                                                return e;
                                                                            }
                                                                        : (dataReader, e) =>
                                                                        {
                                                                            if (reader.IsDBNull(fieldIndex))
                                                                                return e;
                                                                            propertySetter(e,
                                                                                reader.GetDateTime(fieldIndex));
                                                                            return e;
                                                                        }
                                                                    : (dataReader, e) =>
                                                                    {
                                                                        propertySetter(e,
                                                                            reader.GetDateTime(fieldIndex));
                                                                        return e;
                                                                    }
                                                                : (dataReader, e) =>
                                                                {
                                                                    if (reader.IsDBNull(fieldIndex))
                                                                        return e;
                                                                    propertySetter(e, reader.GetInt64(fieldIndex));
                                                                    return e;
                                                                }
                                                            : (dataReader, e) =>
                                                            {
                                                                propertySetter(e, reader.GetInt64(fieldIndex));
                                                                return e;
                                                            }
                                                        : (dataReader, e) =>
                                                        {
                                                            if (reader.IsDBNull(fieldIndex))
                                                                return e;
                                                            propertySetter(e, reader.GetInt32(fieldIndex));
                                                            return e;
                                                        }
                                                    : (dataReader, e) =>
                                                    {
                                                        propertySetter(e, reader.GetInt32(fieldIndex));
                                                        return e;
                                                    }
                                                : (dataReader, e) =>
                                                {
                                                    if (reader.IsDBNull(fieldIndex))
                                                        return e;
                                                    propertySetter(e, reader.GetInt16(fieldIndex));
                                                    return e;
                                                }
                                            : (dataReader, e) =>
                                            {
                                                propertySetter(e, reader.GetInt16(fieldIndex));
                                                return e;
                                            } : (dataReader, e) =>
                                        {
                                            if (reader.IsDBNull(fieldIndex))
                                                return e;
                                            propertySetter(e, reader.GetString(fieldIndex));
                                            return e;
                                        }
                                        : (dataReader, e) =>
                                        {
                                            if (reader.IsDBNull(fieldIndex))
                                                return e;
                                            propertySetter(e, reader.GetDecimal(fieldIndex));
                                            return e;
                                        }
                                    : (dataReader, e) =>
                                    {
                                        propertySetter(e, reader.GetDecimal(fieldIndex));
                                        return e;
                                    };
                            }
                            else
                            {
                                funcArray[i] = (dataReader, e) =>
                                {
                                    if (reader.IsDBNull(fieldIndex))
                                        return e;
                                    propertySetter(e, reader.GetValue(fieldIndex));
                                    return e;
                                };
                            }
                        }
                    }
                }

                foreach (var func in funcArray)
                    if (func != null)
                    {
                        var obj2 = func(reader, obj1);
                    }

                lentity.Add(obj1);
            }

            return lentity;
        }

        public static Action<T, object> BuildUntypedSetter<T>(PropertyInfo propertyInfo)
        {
            var declaringType = propertyInfo.DeclaringType;
            var setMethod = propertyInfo.GetSetMethod();
            var parameterExpression1 = Expression.Parameter(declaringType, "t");
            var parameterExpression2 = Expression.Parameter(typeof(object), "p");
            return Expression
                .Lambda<Action<T, object>>(
                    Expression.Call(parameterExpression1, setMethod,
                        (Expression) Expression.Convert(parameterExpression2, propertyInfo.PropertyType)),
                    parameterExpression1, parameterExpression2).Compile();
        }

        public static TEntity MapSelectorWithVales<TEntity>(Dictionary<string, object> valuePairs)
            where TEntity : class, new()
        {
            var entity = new TEntity();
            foreach (var property in typeof(TEntity).GetProperties())
            {
                var propertyInfo = property;
                var keyValuePair = valuePairs.Where(p => p.Key.ToLower() == propertyInfo.Name.ToLower())
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(keyValuePair.Key))
                    BuildUntypedSetter<TEntity>(propertyInfo)(entity, keyValuePair.Value);
            }

            return entity;
        }
    }
}