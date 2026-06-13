using CorporateCashFlow.Entity.Entities;

namespace CorporateCashFlow.Repository.IRepository.Base;
public interface IRepositoryBase<TEntity> where TEntity : class, IEntity
{
    Task<TEntity> InsertAsync(TEntity entidade, CancellationToken cancellationToken = default);

    Task<IEnumerable<TEntity>> InsertAsync(IEnumerable<TEntity> entidades, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entidade, CancellationToken cancellationToken = default);

    Task UpdateAsync(IEnumerable<TEntity> entidades, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entidade, CancellationToken cancellationToken = default);

    Task RemoveAsync(IEnumerable<TEntity> entidades, CancellationToken cancellationToken = default);

    IQueryable<TEntity> GetQueryable();

    Task<IEnumerable<TEntity>> ObterTodosAsNoTracking();

    Task<IEnumerable<TEntity>> ObterTodos();

    Task<TEntity> FilterById(object id);

    void RemoveEntityContext(object id);

    IEnumerable<Tprop> GetPropEntityBy<Tprop>(Func<TEntity, bool> predicate, Func<TEntity, Tprop> predicatePropReturn);
}
