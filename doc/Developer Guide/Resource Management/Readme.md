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

There is a setting that can affect the open and close sequence of resources. Under Engine settings, change resource strategy to "Short Lived Connections". This ensure connections always closes before test plan ends.

`TestPlan     -----------------------------------------------------------------`
`TestPlan     Starting TestPlan 'Untitled' on 09/30/2020 13:53:54, 1 of 1 TestSteps enabled.`
`Gen          Connection to Simulate successful`
`Gen          Resource "Gen" opened. [66.9 us]`
`BaseInstr    Opening Base Instrument`
`BaseInstr    NormSubInstr connected: True`
`BaseInstr    IgnoreSubInstr connected: False`
`BaseInstr    Resource "BaseInstr" opened. [2.00 s]`
`TestPlan     "Resource Open Before Example" started.`
`BaseInstr    Closing Base Instrument`
`BaseInstr    NormSubInstr connected: True`
`BaseInstr    IgnoreSubInstr connected: False`
`BaseInstr    Resource "BaseInstr" closed. [3.00 s]`
`Gen          Resource "Gen" closed. [66.4 us]`
`TestPlan     "Resource Open Before Example" completed. [5.00 s]`
`Summary      ----- Summary of test plan started 09/30/2020 13:53:54 -----`
`Summary       Resource Open Before Example                     5.00 s    `     
`Summary      ------------------------------------------------------------`
`Summary      -------- Test plan completed successfully in 5.01 s --------`