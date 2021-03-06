﻿using System.Data;
using System.Data.Common;

namespace EasyDapper.Abstractions
{
    public interface IConnectionProvider
    {
        string GetConnectionString { get; }

        DbConnection GetDbConnection { get; }

        IDbTransaction GetDbTransaction { get; }

        TConnection Provide<TConnection>() where TConnection : class, IConnection;

        IDbTransaction BeginTransaction();
    }
}