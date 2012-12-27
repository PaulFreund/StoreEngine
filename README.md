Model Definition
----------------

First create a struct for the Model and a Provider to serve it, there are ready to use base classes to derive from:

* LocalStorage from Winrt
* XML Based file in application data folder
* File based in application data folder

You then use the [Persist] attribute to select the CacheMode and the [PersistValue] attribute to select which 
properties should be preserved and how they should be initialized 

```c#
public class ExampleProvider : IStoreProviderLocalStorage<ExampleModel>
{
    public ExampleProvider(string containerPath) : base(containerPath) { }
}

[Persist(PersistType.Cache)]
public struct ExampleModel
{
    [PersistValue(false)] 
    public bool exampleBoolean { get; set; }
      
    [PersistValue("InitValue")] 
    public string exampleString { get; set; }
}
```

Store construction
------------------

After you designed your Provider and Model you can create the Store, and register the Provider

```c#

StoreEngine.Store MyStore = new StoreEngine.Store();

MyStore.RegisterProvider("Examples", new ExampleProvider("ExampleData"));
```

The store is address based, and the Models are now available with the path Examples.* , 
inside the LocalStore the objects will be located in a Container called "ExampleData" (see Constructor of Provider)

Store modification
------------------

Modifications of the Store is now very easy

```c#

var firstExample = MyStore.Create<ExampleModel>("Examples.FirstExample");
var secondExample = MyStore.Create<ExampleModel>("Examples.SecondExample");
var thirdExample = MyStore.Create<ExampleModel>("Examples.ThirdExample");

firstExample.exampleString = "Hi!";
MyStore.Update("Examples.FirstExample", firstExample);

MyStore.Delete("Examples.ThirdExample");

var tmpExample = Mystore.Read<ExampleModel>("Examples.FirstExample");
secondExample.exampleString = tmpExample.exampleString;
Mystore.Update("Examples.SecondExample", secondExample);
```

Limitations
-----------

* Only structs can be used as Models
* Only properties can be persisted
* At the moment only string, bool, int and enums will work
