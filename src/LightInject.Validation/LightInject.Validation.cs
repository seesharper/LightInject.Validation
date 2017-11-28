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

using System.Collections.ObjectModel;
using System.Reflection;
using Type = System.Type;

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
    using ServiceMap = System.Collections.Generic.Dictionary<Type, System.Collections.Generic.Dictionary<string, ServiceRegistration>>;

    public static class ContainerExtensions
    {
        private static readonly ConcurrentDictionary<Type, int> LifeSpans = new ConcurrentDictionary<Type, int>();

        private const string NotDisposeMessage =
            @"The service {0} being injected as a constructor argument into {1} implements IDisposable" +
            "but is registered without a lifetime (transient). LightInject will not be able to dispose the instance represented by {0}" +
            "If the intent was to manually control the instantiation and destruction, inject Func<{0}> instead." +
            "Otherwise register `{0}` with a lifetime (PerContainer, PerRequest or PerScope).";

        private const string MissingDependency =
                "Class: 'NameSpace.Foo', Parameter 'NameSpace.IBar bar' -> The injected service NameSpace IBar is not registered."
            ;

        static ContainerExtensions()
        {
            LifeSpans.TryAdd(typeof(PerRequestLifeTime), 10);
            LifeSpans.TryAdd(typeof(PerScopeLifetime), 20);
            LifeSpans.TryAdd(typeof(PerContainerLifetime), 30);
        }

        public static IEnumerable<ValidationResult> Validate(this ServiceContainer container)
        {
            var serviceMap = container.AvailableServices.GroupBy(sr => sr.ServiceType).ToDictionary(gr => gr.Key,
                gr => gr.ToDictionary(sr => sr.ServiceName, sr => sr, StringComparer.OrdinalIgnoreCase));


            var verifyableServices = container.AvailableServices.Where(sr => sr.ImplementingType != null);

            return verifyableServices.SelectMany(sr =>
                ValidateConstructor(serviceMap, sr, container.ConstructorSelector.Execute(sr.ImplementingType)));
        }

        private static IReadOnlyCollection<ValidationResult> ValidateConstructor(ServiceMap serviceMap,
            ServiceRegistration serviceRegistration, ConstructorInfo constructorInfo)
        {
            var result = new Collection<ValidationResult>();

            foreach (var parameter in constructorInfo.GetParameters())
            {
                var dependencyRegistration =
                    GetServiceRegistration(serviceMap, new ValidationTarget(parameter), result);
                if (dependencyRegistration == null)
                {
                    return result;
                }

                


                var lifeTimeValdation = ValidateLifetime(serviceRegistration, dependencyRegistration, parameter);
                if (lifeTimeValdation != null)
                {
                    result.Add(lifeTimeValdation);
                }




                if (dependencyRegistration.Lifetime == null)
                {
                    if (dependencyRegistration.ServiceType.Implements<IDisposable>())
                    {
                        result.Add(new ValidationResult(
                             $"The service type '{dependencyRegistration.ServiceType}' implements 'IDisposable'",
                             ValidationSeverity.NotDisposed, parameter));

                    }

                    else if (dependencyRegistration.ImplementingType != null && dependencyRegistration.ImplementingType.Implements<IDisposable>())
                    {
                        result.Add(new ValidationResult(
                            $"The service type '{dependencyRegistration.ServiceType}' implements 'IDisposable'",
                            ValidationSeverity.NotDisposed, parameter));
                    }
                }

            }
            return result;
        }
                    

        private static ValidationResult ValidateLifetime(ServiceRegistration serviceRegistration,
            ServiceRegistration dependencyRegistration, ParameterInfo parameter)
        {
            if (GetLifespan(serviceRegistration.Lifetime) > GetLifespan(dependencyRegistration.Lifetime))
            {
                return new ValidationResult("Missing dependency", ValidationSeverity.Captive, parameter);
            }
            return null;
        }

        public static void SetLifespan<TLifetime>(int lifeSpan) where TLifetime : ILifetime
        {
            LifeSpans.AddOrUpdate(typeof(TLifetime), t => lifeSpan, (t, l) => lifeSpan);
        }


                

        private static ServiceRegistration GetServiceRegistration(ServiceMap serviceMap, ValidationTarget validationTarget, ICollection<ValidationResult> result)
        {

            if (!serviceMap.TryGetValue(validationTarget.ServiceType, out var registrations))
            {
                if (validationTarget.ServiceType.IsFunc() || validationTarget.ServiceType.IsLazy())
                {
                    var underLyingRegistration =  GetServiceRegistration(serviceMap,
                        validationTarget.WithServiceDescription(validationTarget.ServiceType.GenericTypeArguments[0],
                            string.Empty), result);
                    if (underLyingRegistration == null)
                    {
                        result.Add(new ValidationResult("",ValidationSeverity.MissingDependency, validationTarget.Parameter));
                    }
                }
                else
                {
                    result.Add(new ValidationResult("", ValidationSeverity.MissingDependency, validationTarget.Parameter));
                }
                return null;
            }

            if (registrations.TryGetValue(string.Empty, out var registration))
            {
                return registration;
            }

            if (registrations.Count == 1)
            {
                return registrations.Values.First();
            }

            if (registrations.TryGetValue(validationTarget.ServiceName, out registration))
            {
                return registration;
            }

            if (registrations.Count > 1)
            {
                result.Add(new ValidationResult("", ValidationSeverity.Ambiguous, validationTarget.Parameter));
            }


            
            result.Add(new ValidationResult("", ValidationSeverity.MissingDependency,validationTarget.Parameter));
            return null;
        }



        private static string GetLifetimeName(ILifetime lifetime)
        {
            if (lifetime == null)
            {
                return "Transient";
            }
            return lifetime.GetType().Name;
        }


        private static int GetLifespan(ILifetime lifetime)
        {
            if (lifetime == null)
            {
                return 0;
            }
            if (LifeSpans.TryGetValue(lifetime.GetType(), out var lifespan))
            {
                return lifespan;
            }
            return 0;
        }



    }




    public class ValidationTarget
    {
        public ParameterInfo Parameter { get; }
        public Type ServiceType { get; }
        public string ServiceName { get; }


        public ValidationTarget(ParameterInfo parameter) : this(parameter, parameter.ParameterType, parameter.Name)
        {
        }


        public ValidationTarget(ParameterInfo parameter, Type serviceType, string serviceName)
        {
            Parameter = parameter;
            ServiceType = serviceType;
            ServiceName = serviceName;
           

            if (serviceType.GetTypeInfo().IsGenericType && serviceType.GetTypeInfo().ContainsGenericParameters)
            {
                ServiceType = serviceType.GetGenericTypeDefinition();
            }

        }

        public ValidationTarget WithServiceDescription(Type serviceType, string serviceName)
        {
            return new ValidationTarget(Parameter, serviceType, serviceName);
        }

    }


   


    public class ValidationResult
    {
        public ValidationResult(string message, ValidationSeverity severity, ParameterInfo parameter)
        {
            Message = message;
            Severity = severity;
            Parameter = parameter;
        }

        public string Message { get; }

        public ValidationSeverity Severity { get; }
        public ParameterInfo Parameter { get; }
    }

    public enum ValidationSeverity
    {
        NoIssues,
        Captive,
        NotDisposed,
        MissingDependency,
        Ambiguous
    }

    internal static class TypeExtensions
    {
        public static bool Implements<TBaseType>(this Type type)
        {
            return type.GetTypeInfo().ImplementedInterfaces.Contains(typeof(TBaseType));
        }

        public static bool IsFunc(this Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(Func<>);
        }

        public static bool IsLazy(this Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(Lazy<>);
        }
    }
}
