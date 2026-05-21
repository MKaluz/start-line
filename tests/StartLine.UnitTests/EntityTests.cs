using StartLine.Domain.Common;

namespace StartLine.UnitTests;

public class EntityTests
{
    private class TestEntity : Entity { }

    [Fact]
    public void Entity_NewInstance_HasNonEmptyId()
    {
        var entity = new TestEntity();
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Entity_TwoInstances_HaveDifferentIds()
    {
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();
        Assert.NotEqual(entity1.Id, entity2.Id);
    }
}
