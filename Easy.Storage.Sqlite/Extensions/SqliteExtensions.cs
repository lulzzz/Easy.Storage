﻿// ReSharper disable InconsistentNaming
namespace Easy.Storage.Sqlite.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Easy.Common;
    using Easy.Storage.Common;
    using Easy.Storage.Common.Extensions;
    using Easy.Storage.Sqlite.Connections;
    using Easy.Storage.Sqlite.FTS;
    using Easy.Storage.Sqlite.Models;

    /// <summary>
    /// Provides a set of methods to help working with <see cref="SQLiteConnectionBase"/>.
    /// </summary>
    public static class SQLiteExtensions
    {
        /// <summary>
        /// Gets an instance of the <see cref="Repository{T}"/> for the given <typeparamref name="T"/>.
        /// </summary>
        public static IRepository<T> GetRepository<T>(this SQLiteConnectionBase connection)
        {
            return new Repository<T>(connection, Dialect.Sqlite);
        }

        /// <summary>
        /// Returns the <c>SQLite</c> objects in the database.
        /// </summary>
        public static Task<IEnumerable<SQLiteObject>> GetDatabaseObjectsAsync(this SQLiteConnectionBase connection)
        {
            return connection.QueryAsync<SQLiteObject>(SQLiteSQL.Master);
        }

        /// <summary>
        /// Returns <c>True</c> if a table representing <typeparamref name="T"/> exists on the storage.
        /// </summary>
        public static async Task<bool> ExistsAsync<T>(this SQLiteConnectionBase connection)
        {
            var tableName = Table.Get<T>().Name;
            return await connection.ExecuteScalarAsync<uint>(SQLiteSQL.TableExists, new { tableName }).ConfigureAwait(false) != 0;
        }

        /// <summary>
        /// Returns the information relating to the table represented by the <typeparamref name="T"/> in the <c>SQLite</c> database.
        /// </summary>
        public static Task<SQLiteTableInfo> GetTableInfoAsync<T>(this SQLiteConnectionBase connection)
        {
            return connection.GetTableInfoAsync(Table.Get<T>().Name);
        }

        /// <summary>
        /// Returns the information relating to the <paramref name="tableName"/>.
        /// </summary>
        public static async Task<SQLiteTableInfo> GetTableInfoAsync(this SQLiteConnectionBase connection, string tableName)
        {
            Ensure.NotNullOrEmptyOrWhiteSpace(tableName);

            IEnumerable<dynamic> tableInfo;
            try
            {
                tableInfo = await connection.QueryAsync<dynamic>($"PRAGMA table_info({tableName})").ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException($"Table: {tableName} does not exist.", e);
            }

            var columnsInfo = tableInfo.Select(i =>
            {
                SQLiteDataType columnType;
                string typeStr = i.type.ToString();
                if (typeStr.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                {
                    columnType = SQLiteDataType.INTEGER;
                }
                else if (typeStr.Equals("REAL", StringComparison.OrdinalIgnoreCase))
                {
                    columnType = SQLiteDataType.REAL;
                }
                else if (typeStr.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    columnType = SQLiteDataType.TEXT;
                }
                else if (typeStr.Equals("BLOB", StringComparison.OrdinalIgnoreCase))
                {
                    columnType = SQLiteDataType.BLOB;
                }
                else if (typeStr.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    columnType = SQLiteDataType.NULL;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(i.type), "Invalid column type of: " + typeStr);
                }

                return new SQLiteColumnInfo
                {
                    TableName = tableName,
                    Id = i.cid,
                    Name = i.name,
                    Type = columnType,
                    NotNull = i.notnull == 1,
                    DefaultValue = i.dflt_value,
                    IsPrimaryKey = i.pk == 1
                };
            }).ToArray();

            var databaseObjects = await connection.GetDatabaseObjectsAsync();

            return new SQLiteTableInfo
            {
                TableName = tableName,
                Sql = databaseObjects.Single(x => x.Type == SQLiteObjectType.Table && x.Name == tableName).Sql,
                Columns = columnsInfo
            };
        }

        /// <summary>
        /// Returns records matching the given <paramref name="term"/>.
        /// </summary>
        public static Task<IEnumerable<T>> SearchAsync<T>(this SQLiteConnectionBase connection, ITerm<T> term, bool buffered = true)
        {
            var query = Table.Get<T>().Select.Replace($"{Formatter.Spacer}1 = 1;", $"rowId IN {Formatter.NewLine}({Formatter.NewLine}{Formatter.Spacer}{term}{Formatter.NewLine});");
            return connection.QueryAsync<T>(query, buffered: buffered);
        }

        /// <summary>
        /// Returns every attached database and its alias.
        /// </summary>
        public static async Task<IDictionary<string, FileInfo>> GetAttachedDatabasesAsync(this SQLiteConnectionBase connection)
        {
            return (await connection.QueryAsync<dynamic>(SQLiteSQL.AttachedDatabases))
                        .ToDictionary(r => (string)r.name, r => string.IsNullOrWhiteSpace(r.file) ? null : new FileInfo((string)r.file));
        }
    }
}