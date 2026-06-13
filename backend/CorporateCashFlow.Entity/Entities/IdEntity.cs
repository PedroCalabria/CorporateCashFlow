namespace CorporateCashFlow.Entity.Entities
{
    public abstract class IdEntity<T> : IEntity
    {
        public required T Id { get; set; }
    }
}
