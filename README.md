# MX.Lockbox
collection of locking and synchronization primitives

## Named Mutexes

The purpose of a Named Mutex is to provide a lightweight thread-safe mutex (aka "lock", or "Mutual Exclusion" primitive) implementation that can be obtained from an arbitrary string "name".  
- Named mutexes are obtained from `NamedMutexNamespaces`.
- When a named mutex is obtained, it is represented as an `INamedMutex`
- While a named mutex is held, it is guaranteed that no other named mutex of the same name can be obtained from the same `NamedMutexNamespace`.
- A named mutex may be obtained synchronously or asynchronously
- A `NamedMutexNamespace` is by default case-sensitive, but you may create a case-insensitive `NamedMutexNamespace` if desired
- You may create any number of `NamedMutexNamespace` instances, though a default `Global` namespace is provided
- To release an `INamedMutex`, you must call `.Dispose()`.  Use of the `using` statement is recommended

### Example
```
using (var mutex = NamedMutexNamespace.Global.Obtain("foo")) {
   //critical section code
   
   //end of critical section
}
```

### Async example
```
using (var mutex = await NamedMutexNamespace.Global.ObtainAsync("foo")) {
   //critical section code
   
   //end of critical section
}
```

### Example with timeout and cancellation token
```
try {
   using (var mutex = await NamedMutexNamespace.Global.ObtainAsync("foo", TimeSpan.FromSeconds(10), cancellationToken)) {
      log.LogInformation("mutex obtained")
   }
} catch (TimeoutException e) {
   log.LogError(e, "timeout waiting to obtain mutex")
} catch (OperationCanceledException e) {
   log.LogError(e, "User aborted")
}
```

### Example testing without waiting disabled
```
try {
   using (var mutex = NamedMutexNamespace.Global.Obtain("foo", 0)) {
      log.LogInformation("mutex obtained");
   }
} catch (TimeoutException) {
   log.LogInformation("mutex was not available");
}
```
