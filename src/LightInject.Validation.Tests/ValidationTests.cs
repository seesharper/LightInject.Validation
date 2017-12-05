using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace LightInject.Validation.Tests
{
    public class ValidationTests
    {
        [Fact]
        public void ShouldReportCaptiveWhenInjectingTransitiveIntoPerContainer()
        {
            var container = new ServiceContainer();
            container.Register<Foo>(new PerContainerLifetime());
            container.Register<Bar>();

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.Captive);
            result.Should().NotContain(r => string.IsNullOrEmpty(r.Message));
            result.Should().Contain(r => r.ValidationTarget != null);
        }

        [Fact]
        public void ShouldReportCaptiveWhenInjectingPerScopeIntoPerContainer()
        {
            var container = new ServiceContainer();
            container.Register<Foo>(new PerContainerLifetime());
            container.Register<Bar>(new PerScopeLifetime());

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.Captive);
        }

        [Fact]
        public void ShouldReportCaptiveWhenInjectingOpenGenericTransitiveIntoPerContainer()
        {
            var container = new ServiceContainer();
            container.Register(typeof(OpenGenericFoo<>), new PerContainerLifetime());
            container.Register(typeof(OpenGenericBar<>));

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.Captive);
        }

        [Fact]
        public void ShouldReportMissingDependency()
        {
            var container = new ServiceContainer();
            container.Register<Foo<IBar>>();

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.MissingDependency);
        }

        [Fact]
        public void ShoudNotReportMissingDependencyWhenSingleNamedServiceIsRegistered()
        {
            var container = new ServiceContainer();
            container.Register<Foo>();
            
            //Possible missing overload in LightInject 
            //Register<Bar>("ServiceName");
            container.Register<Bar>(f => new Bar(), "SomeBar");

            var result = container.Validate();

            result.Should().BeEmpty();
        }


        [Fact]
        public void ShouldReportNotDisposedWhenRegisteringDisposableServiceTypesAsTransient()
        {
            var container = new ServiceContainer();
            container.Register<FooWithDisposableBar>();
            container.Register<IDisposableBar, DisposableBar>();

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.NotDisposed);
        }

        [Fact]
        public void ShouldReportNotDisposedWhenImplementingTypeIsDisposable()
        {
            var container = new ServiceContainer();
            container.Register<FooWithBarImplementingIDisposable>();
            container.Register<IBar,BarImplementingIDisposable>();

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.NotDisposed);
        }

        [Fact]
        public void ShouldNotReportMissingDependencyWhenInjectingFuncForRegisteredService()
        {
            var container = new ServiceContainer();
            container.Register<Foo<Func<IBar>>>();
            container.Register<IBar, Bar>();

            var result = container.Validate().ToArray();

            result.Should().NotContain(r => r.Severity == ValidationSeverity.MissingDependency);

        }

        [Fact]
        public void ShouldReportMissingDependencyWhenInjectingFuncForUnRegisteredService()
        {
            var container = new ServiceContainer();
            container.Register<Foo<Func<IBar>>>();            

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.MissingDependency);

        }

        [Fact]
        public void ShouldNotReportMissingDependencyWhenInjectingLazyForRegisteredService()
        {
            var container = new ServiceContainer();
            container.Register<Foo<Lazy<IBar>>>();
            container.Register<IBar, Bar>();

            var result = container.Validate().ToArray();

            result.Should().NotContain(r => r.Severity == ValidationSeverity.MissingDependency);
        }

        [Fact]
        public void ShouldReportMissingDependencyWhenInjectingLazyForUnRegisteredService()
        {
            var container = new ServiceContainer();
            container.Register<Foo<Func<IBar>>>();

            var result = container.Validate().ToArray();

            result.Should().Contain(r => r.Severity == ValidationSeverity.MissingDependency);
        }

        [Fact]
        public void ShouldReportAmbigiousService()
        {
            var container = new ServiceContainer();
            container.Register<IBar, Bar>("SomeBar");
            container.Register<IBar, AnotherBar>("AnotherBar");
            container.Register<Foo<IBar>>();

            var result = container.Validate();

            result.Should().Contain(r => r.Severity == ValidationSeverity.Ambiguous);
        }

        [Fact]
        public void ShouldBeAbleToRegisterCustomLifetime()
        {
            var container = new ServiceContainer();
            // Put the custom lifetime between PerScope and PerContainer               
            Validation.SetLifespan<CustomLifetime>(25);

            container.Register<Foo>(new PerContainerLifetime());
            container.Register<Bar>(new CustomLifetime());

            var result = container.Validate();

            result.Should().Contain(r =>
                r.Message.Contains("CustomLifetime") && r.Severity == ValidationSeverity.Captive);

        }


        [Fact]
        public void Test()
        {
            var container = new ServiceContainer();
            container.RegisterInstance(42, "");
            container.RegisterInstance(84, "value");
            container.Register<TestClass>();
            container.GetInstance<TestClass>();
        }


        public class TestClass
        {
            public TestClass(int value)
            {

            }
        }


        //public void ShouldWarnAboutMixedServiceTypeAndImplementingTypeRegistrations()
        //{
        //    var container = new ServiceContainer();
        //    container.Register<IFoo, OpenGenericFoo>();
        //    container.Register<AnotherFoo>();
        //    string message = null;
        //    //container.Validate(m => message = m);            
        //}
    }

    public class Foo<TDependency>
    {
        public Foo(TDependency dependency)
        {
        }
    }


    public class OpenGenericFoo<T>
    {
        public OpenGenericFoo(OpenGenericBar<T> openGenericBar)
        {
        }
    }

    public class OpenGenericBar<T>
    {
        
    }


    public interface IFoo
    {
        
    }

    public class Foo : IFoo
    {
        public Foo(Bar bar)
        {
        }        
    }

    public class Bar : IBar
    {
        
    }

    public class AnotherBar : IBar
    {
        
    }
    


   

    public class DisposableBar : IDisposableBar
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class FooWithDisposableBar
    {
        public FooWithDisposableBar(IDisposableBar disposableBar)
        {
        }
    }

    public class FooWithBarImplementingIDisposable
    {
        public FooWithBarImplementingIDisposable(IBar bar)
        {
        }
    }


    public interface IBar
    {
        
    }

    public interface IDisposableBar : IDisposable
    {

    }

    public class BarImplementingIDisposable : IBar, IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class BarImplementingIDisposableBar : IDisposableBar
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class CustomLifetime : ILifetime
    {
        public object GetInstance(Func<object> createInstance, Scope scope)
        {
            throw new NotImplementedException();
        }
    }
}
