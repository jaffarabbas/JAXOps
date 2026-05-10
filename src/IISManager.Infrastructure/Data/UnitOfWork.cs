using System.Data;
using IISManager.Domain.Interfaces;

namespace IISManager.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDatabaseFactory _dbFactory;
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;

    public UnitOfWork(IDatabaseFactory dbFactory) => _dbFactory = dbFactory;

    public IDbTransaction Begin()
    {
        _connection = _dbFactory.CreateConnection();
        _transaction = _connection.BeginTransaction();
        return _transaction;
    }

    public void Commit()
    {
        _transaction?.Commit();
        Dispose();
    }

    public void Rollback()
    {
        _transaction?.Rollback();
        Dispose();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
        _transaction = null;
        _connection = null;
    }
}
