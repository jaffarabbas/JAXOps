using System.Data;

namespace IISManager.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IDbTransaction Begin();
    void Commit();
    void Rollback();
}
