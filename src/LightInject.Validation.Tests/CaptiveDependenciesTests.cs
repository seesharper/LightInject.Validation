using System;
using Xunit;

namespace LightInject.Validation.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Validate_TransitiveIntoPerContainerLifetime_InvokesWarningAction()
        {
            var container = new ServiceContainer();
            container.Register<Foo>();
            container.Register<Bar>(new PerContainerLifetime());
            string message = null;
            container.Validate(m => message = m);
            Assert.NotNull(message);
        }
    }

    public class Foo
    {
    }

    public class Bar
    {
        public Bar(Foo foo)
        {
        }
    }
}
