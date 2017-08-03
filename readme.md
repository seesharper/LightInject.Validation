## LightInject.Validation

An extension to LightInject that validates the container registrations



### Captive dependencies

A service with a shorter lifetime is injected into a service with a longer lifetime. 

```c#
public class Foo 
{ 
}
public class Bar
{
	private Foo foo	
  
  	public Bar(Foo foo) 
    {
    	this.foo = foo;		  
	}  
}
```

The following is okay

```c#
container.Register<Foo>(new PerContainerLifetime());
container.Register<Bar>();
```

The following is not okay

```c#
container.Register<Foo>();
container.Register<Bar>(new PerContainerLifetime());
```



* Missing dependencies
* Disposable transients