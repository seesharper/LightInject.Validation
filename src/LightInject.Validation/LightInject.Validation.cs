/*********************************************************************************
    The MIT License (MIT)

    Copyright (c) 2016 bernhard.richter@gmail.com

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
******************************************************************************
    LightInject.Validation version 1.0.1
    http://www.lightinject.net/
    http://twitter.com/bernhardrichter
******************************************************************************/

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "Reviewed")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:PrefixLocalCallsWithThis", Justification = "No inheritance")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "Single source file deployment.")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1633:FileMustHaveHeader", Justification = "Custom header.")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "All public members are documented.")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Performance")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("MaintainabilityRules", "SA1403", Justification = "One source file")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("DocumentationRules", "SA1649", Justification = "One source file")]
namespace LightInject.Validation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public static class ContainerExtensions
    {
        private static ConcurrentDictionary<Type, int> lifeSpans = new ConcurrentDictionary<Type, int>();

        static ContainerExtensions()
        {
            lifeSpans.TryAdd(typeof(PerRequestLifeTime), 10);
            lifeSpans.TryAdd(typeof(PerScopeLifetime), 20);
            lifeSpans.TryAdd(typeof(PerContainerLifetime), 30);
        }
    
        public static void Validate(this ServiceContainer container, Action<string> warnAction)
        {            
            var constructorSelector = container.ConstructorSelector;
            var serviceMap = container.AvailableServices.ToDictionary(sr => (sr.ServiceType, sr.ServiceName));//                        
            foreach (var service in container.AvailableServices)
            {
                ValidateService(serviceMap, service, constructorSelector, warnAction);                        
            }
        }

        public static void SetLifespan<TLifetime>(int lifeSpan) where TLifetime : ILifetime
        {
            lifeSpans.AddOrUpdate(typeof(TLifetime), t => lifeSpan, (t,l) => lifeSpan);
        }

        private static void ValidateService(Dictionary<(Type, string), ServiceRegistration> servicemap ,ServiceRegistration registration, IConstructorSelector constructorSelector, Action<string> warnAction)
        {
            if (registration.ImplementingType != null)
            {
                var constructor = constructorSelector.Execute(registration.ImplementingType);
                var parameters = constructor.GetParameters();
                foreach (var parameter in parameters)
                {
                    if (servicemap.TryGetValue((parameter.ParameterType, string.Empty), out var dependency))
                    {
                        if (GetLifespan(registration.Lifetime, warnAction) > GetLifespan(dependency.Lifetime, warnAction))
                        {
                            warnAction($"The dependency {dependency} is being injected into {registration} that has a longer lifetime");
                        }
                    }
                }
            }   
        }

        private static int GetLifespan(ILifetime lifetime, Action<string> warnAction)
        {
            if (lifetime == null)
            {
                return 0;
            }
            if (lifeSpans.TryGetValue(lifetime.GetType(), out var lifespan))
            {
                return lifespan;
            }
            warnAction(
                $"The lifetime {lifespan.GetType()} does not have an expected lifespan. Use the SetLifespan method to specify the lifespan.");
            return 0;
        }
        
    }
}
