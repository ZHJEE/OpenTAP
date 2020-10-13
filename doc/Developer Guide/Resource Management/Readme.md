Resource Management
===================
OpenTAP comes with ResourceOpen attribute that is used to control how and if referenced resources are opened. This attribute is attached to a resource property and there are three modes namely Before, InParallel and Ignore.

-	**Resource Open Before** This mode indicates that the resources pointed to by this property will be opened in sequence, so any referenced resources are open before Open() and until after Close(). This is the default behaviour.
-	**Resource Open Parallel** This mode indicates that a resource property on a resource can be opened in parallel with the resource itself.
-	**Resource Open Ignore** This mode indicates that a resource referenced by this property will not be opened or closed.

For examples of Resource Management, see:

-	`TAP_PATH\Packages\SDK\Examples\PluginDevelopment\TestSteps\Attributes\ResourceOpenAttributeExample`
Both examples have a Base instrument that depends on a Sub instrument which has its respective resource open attribute.

## Resource Open
Upon running *ResourceOpenBeforeAttributeExample* test step in a test plan, the sub instrument will invoke its Open method first before the base instrument's Open method. A delay is added to demonstrate the connection status of sub instrument connecting first before that of base instrument.

When the test plan stops, the base instrument's Close method will be invoked and disconnected. A delay is added to demonstrate that the sub instrument will invoke its Close method after base instrument has been disconnected.

## Resource Parallel
Upon running *ResourceOpenParallelAttributeExample* test step in test plan, the base instrument will open in parallel with sub instrument. To demonstrate that sub instrument is being invoked to open, a delay is added to delay the open connection of the base instrument.

When the test plan stops, the sub instrument will disconnect and close its connection. Subsequently, a delay is added to demonstrate that base instrument will disconnect and close after sub instrument is disconnected.

## Resource Ignore
For resources with the above attribute, test plan will ignore such resources and exclude them in the test execution. Hence, these resources will neither invoke its Open nor Close methods.

There is a setting that can affect the open and close sequence of resources. Under Engine settings, change resource strategy from "Default Resource Manager" to "Short Lived Connections".

`09:14:45.593  TestPlan     -----------------------------------------------------------------`
`09:14:45.594  TestPlan     Starting TestPlan 'Untitled' on 10/13/2020 09:14:45, 1 of 1 TestSteps enabled.`
`09:14:45.606  TwoPortInst  Opening TwoPortInstrument.`
`09:14:45.606  TwoPortInst  Resource "TwoPortInst" opened. [65.4 us]`
`09:14:47.606  INST         Opening Prior Instrument`
`09:14:47.606  INST         PriorSubInstr connected: True`
`09:14:47.606  INST         IgnoreSubInstr connected: False`
`09:14:47.606  INST         Resource "INST" opened. [2.00 s]`
`09:14:47.606  TestPlan     "Resource Open Before Example" started.`
`09:14:48.607  INST         Closing Prior Instrument`
`09:14:50.607  INST         PriorSubInstr connected: True`
`09:14:50.607  INST         IgnoreSubInstr connected: False`
`09:14:50.607  INST         Resource "INST" closed. [3.00 s]`
`09:14:50.607  TwoPortInst  Closing TwoPortInstrument.`
`09:14:50.607  TwoPortInst  Resource "TwoPortInst" closed. [50.1 us]`
`09:14:50.607  TestPlan     "Resource Open Before Example" completed. [5.00 s]`
`09:14:50.613  Summary      ----- Summary of test plan started 10/13/2020 09:14:45 -----`
`09:14:50.613  Summary       Resource Open Before Example                     5.00 s`         
`09:14:50.613  Summary      ------------------------------------------------------------`
`09:14:50.613  Summary      -------- Test plan completed successfully in 5.01 s --------`


From the above log, since the connections are supposed to be short lived. Resource connections will always close before test plan ends. However, for the previous case of "Default Resource Manager", the resource connections will be closed only after the test plan has ended.
Using short lived connections result in more efficient resource management since connections are closed when no longer needed by the test.