### 2.0.0
* AsyncTracing is no longer auto-open
* Upgrade to F# 4.0

### 1.0.1

* Check if exception is already tracked before setting the "tracked" data item.
* Fix some issues with the PCL library not working properly (throwing exception on startup).
* ITraceSource no longer contains the "Wrapped" member directly to be consistent with the PCL definition, use IExtendedTraceSource if required.

### 1.0.0

 * Initial release
