Resource Management
===================
OpenTAP comes with ResourceOpen attribute that is used to control how and if referenced resources are opened. This attribute is attached to a resource property and there are three modes namely Before, InParallel and Ignore.

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
For resources with the above attribute, test plan will remove such resources and exclude them in the test execution. Hence, these resources will neither invoke its Open nor Close methods.