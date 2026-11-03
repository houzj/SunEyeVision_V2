using System;
using System.Collections.Generic;


namespace SunEyeVision.Domain.Solutions;

/// <summary>
/// 数据库类型枚举
/// </summary>
public enum DatabaseType
{
    /// <summary>
    /// MySQL
    /// </summary>
    MySQL,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSQL,

    /// <summary>
    /// SQL Server
    /// </summary>
    SQLServer,

    /// <summary>
    /// Oracle
    /// </summary>
    Oracle,

    /// <summary>
    /// SQLite
    /// </summary>
    SQLite,

    /// <summary>
    /// 其他
    /// </summary>
    Other
}

/// <summary>
/// 数据库配置模型
/// </summary>
/// <remarks>
/// 用于配置数据库连接，支持多种数据库类型。
/// </remarks>
public class DatabaseConfiguration
{
    private bool _enabled = false;
    private DatabaseType _type = DatabaseType.MySQL;
    private string _connectionString = "";
    private int _connectionTimeout = 30;
    private int _maxPoolSize = 100;
    private int _minPoolSize = 0;

    /// <summary>
    /// 是否启用数据库
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        internal set => _enabled = value;
    }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType Type
    {
        get => _type;
        internal set => _type = value;
    }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString
    {
        get => _connectionString;
        internal set => _connectionString = value;
    }

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeout
    {
        get => _connectionTimeout;
        internal set => _connectionTimeout = value;
    }

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize
    {
        get => _maxPoolSize;
        internal set => _maxPoolSize = value;
    }

    /// <summary>
    /// 最小连接池大小
    /// </summary>
    public int MinPoolSize
    {
        get => _minPoolSize;
        internal set => _minPoolSize = value;
    }

    /// <summary>
    /// 数据表配置
    /// </summary>
    public Dictionary<string, object> Tables { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public DatabaseConfiguration()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public DatabaseConfiguration(DatabaseType type, string connectionString)
    {
        Type = type;
        ConnectionString = connectionString;
    }

    /// <summary>
    /// 克隆数据库配置
    /// </summary>
    public DatabaseConfiguration Clone()
    {
        return new DatabaseConfiguration
        {
            Enabled = Enabled,
            Type = Type,
            ConnectionString = ConnectionString,
            ConnectionTimeout = ConnectionTimeout,
            MaxPoolSize = MaxPoolSize,
            MinPoolSize = MinPoolSize,
            Tables = new Dictionary<string, object>(Tables),
            CreatedTime = CreatedTime,
            ModifiedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 验证数据库配置
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (Enabled)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                errors.Add("连接字符串不能为空");
            }

            if (ConnectionTimeout <= 0)
            {
                errors.Add("连接超时时间必须大于0");
            }

            if (MaxPoolSize < MinPoolSize)
            {
                errors.Add("最大连接池不能小于最小连接池");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 获取数据库名称
    /// </summary>
    public string GetDatabaseName()
    {
        switch (Type)
        {
            case DatabaseType.MySQL:
                return "MySQL";
            case DatabaseType.PostgreSQL:
                return "PostgreSQL";
            case DatabaseType.SQLServer:
                return "SQL Server";
            case DatabaseType.Oracle:
                return "Oracle";
            case DatabaseType.SQLite:
                return "SQLite";
            default:
                return "Unknown";
        }
    }
}

