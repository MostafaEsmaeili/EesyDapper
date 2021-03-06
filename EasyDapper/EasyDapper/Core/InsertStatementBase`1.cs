﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EasyDapper.Abstractions;
using EasyDapper.Core.Abstractions;
using EasyDapper.Core.CustomAttribute;

namespace EasyDapper.Core
{
    public abstract class InsertStatementBase<TEntity> : SqlStatement<TEntity, TEntity>, IInsertStatement<TEntity>,
        ISqlStatement<TEntity>, IClauseBuilder
        where TEntity : class, new()
    {
        protected readonly IList<Expression<Func<TEntity, object>>> selectors =
            new List<Expression<Func<TEntity, object>>>();

        protected readonly Dictionary<string, object> selectorswithValue = new Dictionary<string, object>();
        protected readonly IList<object> values = new List<object>();
        protected readonly IWritablePropertyMatcher writablePropertyMatcher;
        protected TEntity entity;
        protected string IdentityFiled = "Id";
        protected bool IsAutoIncrement = true;
        protected bool paramWithMode;

        public InsertStatementBase(
            IStatementExecutor statementExecutor,
            IEntityMapper entityMapper,
            IWritablePropertyMatcher writablePropertyMatcher)
            : base(statementExecutor, entityMapper, writablePropertyMatcher)
        {
            this.writablePropertyMatcher = writablePropertyMatcher;
        }

        public IInsertStatement<TEntity> For(TEntity entity)
        {
            if (selectors.Any())
                throw new InvalidOperationException(
                    "For cannot be used once With has been used, please use FromScratch to reset the command before using With.");
            CheckIdentityFiled();
            IsClean = false;
            this.entity = entity;
            return this;
        }

        public IInsertStatement<TEntity> FromScratch()
        {
            selectors.Clear();
            values.Clear();
            entity = default(TEntity);
            IsClean = true;
            return this;
        }

        public override TEntity Go()
        {
            if (paramWithMode)
                throw new InvalidOperationException(
                    "For cannot be used ParamWith have been used, please create a new command.");
            if (IsAutoIncrement)
                using (var reader = StatementExecutor.ExecuteReader(Sql()))
                {
                    return EntityMapper.Map<TEntity>(reader).FirstOrDefault();
                }

            StatementExecutor.ExecuteNonQuery(Sql());
            return entity;
        }

        public override async Task<TEntity> GoAsync()
        {
            if (IsAutoIncrement)
                using (var reader = await StatementExecutor.ExecuteReaderAsync(Sql()))
                {
                    return EntityMapper.Map<TEntity>(reader).FirstOrDefault();
                }

            var num = await StatementExecutor.ExecuteNonQueryAsync(Sql());
            return entity;
        }

        public IInsertStatement<TEntity> UsingTableName(string tableName)
        {
            TableName = tableName;
            return this;
        }

        public IInsertStatement<TEntity> UsingIdField<TMember>(
            Expression<Func<TEntity, TMember>> idField,
            bool IsAutoInc = true)
        {
            var property = Reflect<TEntity>.GetProperty(idField);
            IdentityFiled = !IsAutoInc ? "this_is_no_identityfiled" : property.Name;
            IsAutoIncrement = IsAutoInc;
            return this;
        }

        public IInsertStatement<TEntity> With<TMember>(
            Expression<Func<TEntity, TMember>> selector,
            TMember value)
        {
            if (entity != null)
                throw new InvalidOperationException(
                    "With cannot be used once For has been used, please use FromScratch to reset the command before using With.");
            CheckIdentityFiled();
            IsClean = false;
            var selector1 = ConvertExpression(selector);
            if (!CustomAttributeHandle.IsIdentityField<TEntity>(GetMemberName(selector1)))
            {
                selectors.Add(selector1);
                values.Add(value);
            }

            selectorswithValue.Add(GetMemberName(selector1), value);
            return this;
        }

        public abstract string ParamSql(bool IsOne = true);


        public ValueTuple<string, TEntity> ParamSqlWithEntity()
        {
            return new ValueTuple<string, TEntity>(ParamSql(true), GetEntityFromwithValue());
        }

        public IInsertStatement<TEntity> ParamWith(
            Expression<Func<TEntity, object>> selector,
            params Expression<Func<TEntity, object>>[] additionalSelectors)
        {
            WithParam(selector);
            foreach (var additionalSelector in additionalSelectors)
                WithParam(additionalSelector);
            return this;
        }

        protected void CheckIdentityFiled()
        {
            IdentityFiled = CustomAttributeHandle.IdentityFieldStr<TEntity>(IdentityFiled);
            IsAutoIncrement = typeof(TEntity).GetMember(IdentityFiled).Count() > 0;
        }

        protected abstract string FormatColumnNames(IEnumerable<string> names, string perParam = "");

        protected abstract string FormatColumnNames(IDictionary<string, string> names, string perParam = "");

        protected string FormatValues(IEnumerable<object> values)
        {
            return string.Join(",", values.Select(FormatValue));
        }

        protected TEntity GetEntityFromwithValue()
        {
            return selectors.Any() ? DataReaderEntityMapper.MapSelectorWithVales<TEntity>(selectorswithValue) : entity;
        }

        protected string GetColumnsList(string perParam = "")
        {
            return selectors.Any() ? GetColumnsListFromSelectors(perParam) : GetColumnsListFromEntity(perParam);
        }

        protected string GetColumnsParamList(string perParam = "")
        {
            return selectors.Any()
                ? GetColumnsListParamFromSelectors(perParam)
                : GetColumnsListParamFromEntity(perParam);
        }

        protected string GetColumnsListBack()
        {
            return selectors.Any() ? GetColumnsListFromSelectorsBack() : GetColumnsListFromEntityBack();
        }

        protected string GetColumnsListFromEntity(string perParam)
        {
            return FormatColumnNames(typeof(TEntity).GetProperties().Where(p =>
            {
                if (!p.IsIdField())
                    return writablePropertyMatcher.TestIsDbField(p);
                return false;
            }).Select(p => p.ColumnName()), perParam);
        }

        protected string GetColumnsListParamFromEntity(string perParam)
        {
            return FormatColumnNames(typeof(TEntity).GetProperties().Where(p =>
            {
                if (!p.IsIdField())
                    return writablePropertyMatcher.TestIsDbField(p);
                return false;
            }).ToDictionary(p => p.Name, p => p.ColumnName()), perParam);
        }

        protected string GetColumnsListFromEntityBack()
        {
            return FormatColumnNames(
                typeof(TEntity).GetProperties().Where(p => writablePropertyMatcher.TestIsDbField(p))
                    .ToDictionary(p => p.Name, p => p.ColumnName()), "");
        }

        protected string GetColumnsListFromSelectors(string perParam)
        {
            return FormatColumnNames(selectors.Select(GetMemberColumnName), perParam);
        }

        protected string GetColumnsListParamFromSelectors(string perParam)
        {
            return FormatColumnNames(selectors.ToDictionary(GetMemberName, GetMemberColumnName), perParam);
        }

        protected string GetColumnsListFromSelectorsBack()
        {
            var dictionary = selectors.ToDictionary(GetMemberName, GetMemberColumnName);
            if (IsAutoIncrement &&
                string.IsNullOrWhiteSpace(dictionary.Where(c => c.Key == IdentityFiled).FirstOrDefault().Key))
            {
                var str = GetColumnAlias<TEntity>(IdentityFiled);
                if (string.IsNullOrWhiteSpace(str))
                    str = IdentityFiled;
                dictionary.Add(IdentityFiled, str);
            }

            return FormatColumnNames(dictionary, "");
        }

        protected string GetValuesFromEntity()
        {
            return FormatValues(typeof(TEntity).GetProperties().Where(p =>
            {
                if (!p.IsIdField())
                    return writablePropertyMatcher.TestIsDbField(p);
                return false;
            }).Select(p => p.GetValue(entity)));
        }

        protected string GetValuesList()
        {
            return selectors.Any() ? FormatValues(values) : GetValuesFromEntity();
        }

        protected IInsertStatement<TEntity> WithParam(
            Expression<Func<TEntity, object>> selector)
        {
            paramWithMode = true;
            if (entity != null)
                throw new InvalidOperationException(
                    "With cannot be used once For has been used, please use FromScratch to reset the command before using With.");
            CheckIdentityFiled();
            IsClean = false;
            var selector1 = ConvertExpression(selector);
            selectors.Add(selector1);
            selectorswithValue.Add(GetMemberName(selector1), null);
            return this;
        }
    }
}