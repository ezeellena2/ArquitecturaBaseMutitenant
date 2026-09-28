using System.Linq.Expressions;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

internal static class ModelBuilderExtensions
{
    public const string TenantFilter = "Tenant";
    public const string PublicFilter = "Public";
    public const string PartiesFilter = "Parties";
    public const string SoftDeleteFilter = "SoftDelete";

    public static ModelBuilder ApplyIsolationQueryFilters(this ModelBuilder modelBuilder, ApplicationDbContext context)
    {
        var tenantId = Expression.Property(Expression.Constant(context), nameof(ApplicationDbContext.ActiveTenantId));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entity => entity.BaseType is null).ToArray())
        {
            var type = entityType.ClrType;
            var entity = Expression.Parameter(type, "entity");
            var builder = modelBuilder.Entity(type);

            if (typeof(ITenantOwned).IsAssignableFrom(type))
            {
                var belongsToTenant = TenantEquals(entity, nameof(ITenantOwned.TenantId), tenantId);
                builder.HasQueryFilter(TenantFilter, Expression.Lambda(belongsToTenant, entity));
            }

            if (typeof(IPublishedByBusiness).IsAssignableFrom(type))
            {
                var isPublished = Expression.Property(entity, nameof(IPublishedByBusiness.IsPublished));
                var belongsToTenant = TenantEquals(entity, nameof(IPublishedByBusiness.BusinessTenantId), tenantId);
                builder.HasQueryFilter(PublicFilter, Expression.Lambda(
                    Expression.OrElse(isPublished, belongsToTenant), entity));
            }

            if (typeof(IConsumerBusinessShared).IsAssignableFrom(type))
            {
                var belongsToConsumer = TenantEquals(entity, nameof(IConsumerBusinessShared.ConsumerTenantId), tenantId);
                var belongsToBusiness = TenantEquals(entity, nameof(IConsumerBusinessShared.BusinessTenantId), tenantId);
                builder.HasQueryFilter(PartiesFilter, Expression.Lambda(
                    Expression.OrElse(belongsToConsumer, belongsToBusiness), entity));
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(type))
            {
                var isActive = Expression.Not(Expression.Property(entity, nameof(ISoftDeletable.IsDeleted)));
                builder.HasQueryFilter(SoftDeleteFilter, Expression.Lambda(isActive, entity));
            }
        }

        return modelBuilder;
    }

    private static BinaryExpression TenantEquals(ParameterExpression entity, string propertyName, Expression activeTenantId) =>
        Expression.Equal(Expression.Convert(Expression.Property(entity, propertyName), typeof(Guid?)), activeTenantId);
}
