using System.Reflection;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Common;

public sealed class EntityAndValueObjectTests
{
    private sealed class SampleEntity : Entity
    {
        public SampleEntity()
        {
        }

        public SampleEntity(Guid id) : base(id)
        {
        }
    }

    private sealed class OtherEntity(Guid id) : Entity(id);

    private sealed class SampleValue(string name, int count) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return name;
            yield return count;
        }
    }

    private sealed class OtherValue(string name, int count) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return name;
            yield return count;
        }
    }

    [Fact]
    public void New_entity_gets_a_version_7_guid()
    {
        Assert.Equal(7, new SampleEntity().Id.Version);
    }

    [Fact]
    public void Entity_exposes_a_protected_parameterless_constructor_for_ef()
    {
        var constructor = typeof(Entity).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsFamily);
    }

    [Fact]
    public void Entities_are_equal_only_when_type_and_id_match()
    {
        var id = Guid.CreateVersion7();
        var first = new SampleEntity(id);
        var same = new SampleEntity(id);

        Assert.Equal(first, same);
        Assert.True(first == same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual<Entity>(first, new OtherEntity(id));
        Assert.Throws<ArgumentException>(() => new SampleEntity(Guid.Empty));
    }

    [Fact]
    public void Value_objects_compare_all_components_and_type()
    {
        var first = new SampleValue("Ana", 2);
        var same = new SampleValue("Ana", 2);

        Assert.Equal(first, same);
        Assert.True(first == same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, new SampleValue("Ana", 3));
        Assert.NotEqual<ValueObject>(first, new OtherValue("Ana", 2));
    }
}
